using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ScanCheckSakura.Data;

namespace ScanCheckSakura.Services
{
    public interface IDefectSyncService
    {
        Task UpsertDefectAsync(string workOrder, string serialNumber, string ngCode);
        Task<BackfillResult> BackfillAndSyncAsync(string date);
    }

    public class BackfillResult
    {
        public string Date { get; set; } = string.Empty;
        public int WorkOrdersFound { get; set; }
        public int WorkOrdersResolved { get; set; }
        public int MergedRows { get; set; }
        public List<string> Details { get; set; } = new();
    }

    public class DefectSyncService : IDefectSyncService
    {
        private const string OdooSearchUrl =
            "https://sigmaworldwide.io/web/dataset/call_kw/mrp.production/web_search_read";

        private readonly ApplicationDbContext _db;
        private readonly IHttpClientFactory   _httpFactory;
        private readonly ILogger<DefectSyncService> _logger;

        public DefectSyncService(
            ApplicationDbContext db,
            IHttpClientFactory   httpFactory,
            ILogger<DefectSyncService> logger)
        {
            _db          = db;
            _httpFactory = httpFactory;
            _logger      = logger;
        }

        // ─────────────────────────────────────────────────────────────────
        // Backfill Item_code cho data cũ (Item_code = NULL) theo ngày,
        // sau đó sync toàn bộ vào SVN_Defect_Record
        // ─────────────────────────────────────────────────────────────────
        public async Task<BackfillResult> BackfillAndSyncAsync(string date)
        {
            var result = new BackfillResult { Date = date };

            if (!DateTime.TryParseExact(date, "yyyyMMdd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var targetDate))
            {
                result.Details.Add("Sai định dạng date — cần yyyyMMdd");
                return result;
            }

            var dateStart = targetDate.Date;
            var dateEnd   = dateStart.AddDays(1);

            // 1. Lấy danh sách WorkOrder chưa có Item_code trong ngày đó
            var workOrders = await _db.SM_FQCBP_H
                .Where(h => h.Status    == "NG"
                         && h.Item_code == null
                         && h.Timeline  >= dateStart
                         && h.Timeline  < dateEnd)
                .Select(h => h.WorkOrder)
                .Distinct()
                .ToListAsync();

            result.WorkOrdersFound = workOrders.Count;

            // 2. Gọi Odoo cho từng WO → fill Item_code vào SM_FQCBP_H
            foreach (var wo in workOrders)
            {
                var itemCode = await GetItemCodeFromOdooAsync(wo);
                if (itemCode == null)
                {
                    result.Details.Add($"WO={wo} → Không lấy được Item_code từ Odoo");
                    continue;
                }

                var rows = await _db.SM_FQCBP_H
                    .Where(h => h.WorkOrder == wo
                             && h.Status    == "NG"
                             && h.Item_code == null
                             && h.Timeline  >= dateStart
                             && h.Timeline  < dateEnd)
                    .ToListAsync();

                foreach (var row in rows)
                    row.Item_code = itemCode;

                result.Details.Add($"WO={wo} → Item_code={itemCode} ({rows.Count} rows updated)");
                result.WorkOrdersResolved++;
            }

            await _db.SaveChangesAsync();

            // 3. Full MERGE sync: SM_FQCBP_H → SVN_Defect_Record
            result.MergedRows = await _db.Database.ExecuteSqlRawAsync(@"
                MERGE SVN_Defect_Record AS target
                USING (
                    SELECT
                        h.Item_code,
                        h.NgCode                                     AS Defect_Code,
                        CONVERT(VARCHAR(8), h.Timeline, 112)         AS ScanDate,
                        COUNT(*)                                     AS TotalCount
                    FROM  SM_FQCBP_H h
                    WHERE h.Status    = 'NG'
                      AND h.Item_code IS NOT NULL
                      AND CONVERT(VARCHAR(8), h.Timeline, 112) = @date
                    GROUP BY h.Item_code, h.NgCode, CONVERT(VARCHAR(8), h.Timeline, 112)
                ) AS source
                    ON  target.Item_code   = source.Item_code
                    AND target.Defect_Code = source.Defect_Code
                    AND target.INSDatetime = source.ScanDate
                WHEN MATCHED THEN
                    UPDATE SET
                        Qty_NG    = source.TotalCount,
                        Operation = ISNULL(
                            target.Operation,
                            (SELECT TOP 1 Operation FROM SVN_target
                             WHERE  Date_time = source.ScanDate
                               AND  Operation LIKE '%' + source.Item_code + '%')
                        )
                WHEN NOT MATCHED THEN
                    INSERT (Item_code, Defect_Code, Qty_NG, INSDatetime, Operation, Employer_code, Employer_name)
                    VALUES (
                        source.Item_code,
                        source.Defect_Code,
                        source.TotalCount,
                        source.ScanDate,
                        (SELECT TOP 1 Operation FROM SVN_target
                         WHERE  Date_time = source.ScanDate
                           AND  Operation LIKE '%' + source.Item_code + '%'),
                        NULL, NULL
                    );",
                new SqlParameter("@date", date));

            return result;
        }

        // ─────────────────────────────────────────────────────────────────
        // Main entry point: gọi khi có 1 NG scan thành công
        // ─────────────────────────────────────────────────────────────────
        public async Task UpsertDefectAsync(string workOrder, string serialNumber, string ngCode)
        {
            try
            {
                // 1. Lấy Item_code từ Odoo theo WorkOrder
                var itemCode = await GetItemCodeFromOdooAsync(workOrder);
                if (itemCode == null)
                {
                    _logger.LogWarning("DefectSync: Không lấy được Item_code cho WO={WO}", workOrder);
                    return;
                }

                // 2. Ghi ngược Item_code vào SM_FQCBP_H (để sp_FullSyncDefectByDate có thể dùng)
                var historyRow = await _db.SM_FQCBP_H
                    .Where(h => h.WorkOrder == workOrder
                             && h.SerialNumber == serialNumber
                             && h.NgCode == ngCode
                             && h.Item_code == null)
                    .OrderByDescending(h => h.Timeline)
                    .FirstOrDefaultAsync();

                if (historyRow != null)
                {
                    historyRow.Item_code = itemCode;
                    await _db.SaveChangesAsync();
                }

                // 3. INSDatetime = hôm nay dạng yyyyMMdd
                var insDatetime = DateTime.Now.ToString("yyyyMMdd");

                // 4. Tìm Operation từ SVN_target theo ngày và Item_code
                var operation = await _db.SVN_target
                    .Where(t => t.Date_time == insDatetime
                             && t.Operation != null
                             && t.Operation.Contains(itemCode))
                    .Select(t => t.Operation)
                    .FirstOrDefaultAsync();

                // 5. MERGE vào SVN_Defect_Record
                await _db.Database.ExecuteSqlRawAsync(@"
                    MERGE SVN_Defect_Record AS target
                    USING (
                        SELECT @item_code   AS Item_code,
                               @defect_code AS Defect_Code,
                               @ins_dt      AS INSDatetime
                    ) AS source
                        ON  target.Item_code   = source.Item_code
                        AND target.Defect_Code = source.Defect_Code
                        AND target.INSDatetime = source.INSDatetime
                    WHEN MATCHED THEN
                        UPDATE SET
                            Qty_NG    = ISNULL(target.Qty_NG, 0) + 1,
                            Operation = ISNULL(@operation, target.Operation)
                    WHEN NOT MATCHED THEN
                        INSERT (Item_code, Defect_Code, Qty_NG, INSDatetime, Operation, Employer_code, Employer_name)
                        VALUES (@item_code, @defect_code, 1, @ins_dt, @operation, NULL, NULL);",
                    new SqlParameter("@item_code",   (object?)itemCode   ?? DBNull.Value),
                    new SqlParameter("@defect_code", (object?)ngCode     ?? DBNull.Value),
                    new SqlParameter("@ins_dt",      insDatetime),
                    new SqlParameter("@operation",   (object?)operation  ?? DBNull.Value));

                _logger.LogInformation(
                    "DefectSync: Upsert OK — Item={Item}, Code={Code}, Date={Date}, Operation={Op}",
                    itemCode, ngCode, insDatetime, operation ?? "(null)");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "DefectSync: Lỗi khi upsert — WO={WO}, NgCode={Code}", workOrder, ngCode);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Gọi Odoo search theo WorkOrder → trích xuất Item_code dạng [RM15A-1000NW]
        // ─────────────────────────────────────────────────────────────────
        private async Task<string?> GetItemCodeFromOdooAsync(string workOrder)
        {
            var cookie = await _db.SM_Defect_Cookie
                .Select(c => c.cookie)
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(cookie)) return null;

            var payload = $@"{{
                ""id"": 556,
                ""jsonrpc"": ""2.0"",
                ""method"": ""call"",
                ""params"": {{
                    ""model"": ""mrp.production"",
                    ""method"": ""web_search_read"",
                    ""args"": [],
                    ""kwargs"": {{
                        ""limit"": 1,
                        ""offset"": 0,
                        ""order"": ""name asc"",
                        ""context"": {{
                            ""lang"": ""vi_VN"",
                            ""tz"": ""Asia/Ho_Chi_Minh"",
                            ""uid"": 2,
                            ""allowed_company_ids"": [1],
                            ""bin_size"": true,
                            ""default_company_id"": 1
                        }},
                        ""count_limit"": 10001,
                        ""domain"": [""|"",
                            [""name"",   ""like"", ""{workOrder}""],
                            [""origin"", ""like"", ""{workOrder}""]
                        ],
                        ""fields"": [""name"", ""origin"", ""product_id""]
                    }}
                }}
            }}";

            try
            {
                var client = _httpFactory.CreateClient();
                using var request = new HttpRequestMessage(HttpMethod.Post, OdooSearchUrl)
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("Cookie", cookie);

                var response = await client.SendAsync(request);
                var body     = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (!root.TryGetProperty("result",  out var result))  return null;
                if (!result.TryGetProperty("records", out var records)) return null;
                if (records.ValueKind != JsonValueKind.Array || records.GetArrayLength() == 0) return null;

                var productId = records[0].GetProperty("product_id");
                if (productId.ValueKind != JsonValueKind.Array || productId.GetArrayLength() < 2) return null;

                var productDescription = productId[1].GetString();
                if (string.IsNullOrEmpty(productDescription)) return null;

                // Trích phần trong ngoặc vuông: "[RM15A-1000NW] Sakura..." → "RM15A-1000NW"
                var match = Regex.Match(productDescription, @"^\[([^\]]+)\]");
                return match.Success ? match.Groups[1].Value : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DefectSync: Lỗi gọi Odoo cho WO={WO}", workOrder);
                return null;
            }
        }
    }
}

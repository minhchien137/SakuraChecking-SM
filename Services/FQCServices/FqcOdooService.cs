using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ScanCheckSakura.Data;

namespace ScanCheckSakura.Services
{
    public interface IFqcOdooService
    {
        Task PostCommentBySerialAsync(string serial, string commentBody);

        /// <summary>
        /// Giống PostCommentBySerialAsync nhưng gửi kèm TestLog vào body comment.
        /// </summary>
        Task PostCommentBySerialWithLogAsync(string serial, string commentBody, JsonElement testLog);

        Task PostCommentByLotWithLogAsync(string lotNumber, string commentBody, JsonElement testLog);
    }

    public class FqcOdooService : IFqcOdooService
    {
        private readonly HttpClient _httpClient;
        private readonly ApplicationDbContext _db;
        private readonly ILogger<FqcOdooService> _logger;

        private const string BaseUrl              = "https://sigmaworldwide.io/web/dataset/call_kw";
        private const string StockLotUrl          = $"{BaseUrl}/stock.lot/web_search_read";
        private const string StockTraceabilityUrl = $"{BaseUrl}/stock.traceability.report/get_html";
        private const string PostCommentUrl       = "https://sigmaworldwide.io/mail/message/post";

        public FqcOdooService(HttpClient httpClient, ApplicationDbContext db, ILogger<FqcOdooService> logger)
        {
            _httpClient = httpClient;
            _db         = db;
            _logger     = logger;
        }

        // ── Lấy cookie từ DB ──────────────────────────────
        private async Task<string?> GetCookieAsync()
        {
            var record = await _db.SM_Defect_Cookie.FirstOrDefaultAsync();
            return string.IsNullOrWhiteSpace(record?.cookie) ? null : record.cookie;
        }

        // Post comment cho FQC
        public async Task PostCommentBySerialAsync(string serial, string commentBody)
        {
            // ── Cookie ────────────────────────────────────
            var cookie = await GetCookieAsync();
            if (cookie == null)
            {
                return;
            }

            var lotPayload = new
            {
                id = 18, jsonrpc = "2.0", method = "call",
                @params = new
                {
                    model = "stock.lot", method = "web_search_read",
                    args  = Array.Empty<object>(),
                    kwargs = new
                    {
                        limit = 1, offset = 0, order = "",
                        domain = new object[] { new object[] { "name", "ilike", serial } },
                        fields = new[] { "name", "product_id" }
                    }
                }
            };

            int lotId;
            using (var req1 = new HttpRequestMessage(HttpMethod.Post, StockLotUrl)
                   { Content = JsonContent.Create(lotPayload) })
            {
                req1.Headers.Add("Cookie", cookie);
                var res1 = await _httpClient.SendAsync(req1);

                res1.EnsureSuccessStatusCode();

                var raw1 = await res1.Content.ReadAsStringAsync();

                using var doc1 = JsonDocument.Parse(raw1);
                if (!doc1.RootElement.TryGetProperty("result", out var r)
                    || !r.TryGetProperty("records", out var records)
                    || records.ValueKind != JsonValueKind.Array
                    || records.GetArrayLength() == 0)
                {
                    return;
                }

                lotId = records[0].GetProperty("id").GetInt32();
            }

            // ── Step 2: lotId → woPageId (traceability) ──
            var tracePayload = new
            {
                id = 23, jsonrpc = "2.0", method = "call",
                @params = new
                {
                    args = new object[]
                    {
                        new
                        {
                            lang = "vi_VN", tz = "Asia/Ho_Chi_Minh",
                            uid  = 2, allowed_company_ids = new[] { 1 },
                            active_id = lotId, model = "stock.lot",
                            ttype = (object?)null, auto_unfold = false, lot_name = (object?)null
                        }
                    },
                    model = "stock.traceability.report", method = "get_html",
                    kwargs = new
                    {
                        context = new
                        {
                            lang = "vi_VN", tz = "Asia/Ho_Chi_Minh",
                            uid  = 2, allowed_company_ids = new[] { 1 }
                        }
                    }
                }
            };

            int woPageId;
            using (var req2 = new HttpRequestMessage(HttpMethod.Post, StockTraceabilityUrl)
                   { Content = JsonContent.Create(tracePayload) })
            {
                req2.Headers.Add("Cookie", cookie);
                var res2 = await _httpClient.SendAsync(req2);

                res2.EnsureSuccessStatusCode();

                using var doc2 = await JsonDocument.ParseAsync(await res2.Content.ReadAsStreamAsync());
                var html = doc2.RootElement
                    .GetProperty("result").GetProperty("html").GetString() ?? "";

                // Thử regex gốc
                var match = Regex.Match(html,
                    @"data-active-id=""(\d+)""[^>]*?>\s*(NM/MO/\d+(?:-\d+)?)\s*<",
                    RegexOptions.IgnoreCase);

                if (!match.Success)
                {
                    var matchWide = Regex.Match(html, @"data-active-id=""(\d+)""", RegexOptions.IgnoreCase);
                    var matchMO = Regex.Match(html, @"NM/MO/[\w\-]+", RegexOptions.IgnoreCase);
                    return;
                }

                woPageId = int.Parse(match.Groups[1].Value);
                var workOrder = match.Groups[2].Value;
            }

            // ── Step 3: post comment ──────────────────────
            var commentPayload = new
            {
                id = 13, jsonrpc = "2.0", method = "call",
                @params = new
                {
                    post_data = new
                    {
                        attachment_ids      = Array.Empty<int>(),
                        attachment_tokens   = Array.Empty<string>(),
                        body                = commentBody,
                        message_type        = "comment",
                        partner_ids         = Array.Empty<int>(),
                        canned_response_ids = Array.Empty<int>(),
                        subtype_xmlid       = "mail.mt_comment"
                    },
                    thread_id    = woPageId,
                    thread_model = "mrp.production",
                    context = new
                    {
                        mail_post_autofollow = true,
                        lang = "vi_VN", tz = "Asia/Ho_Chi_Minh",
                        uid  = 2, allowed_company_ids = new[] { 1 }
                    }
                }
            };

            using var req3 = new HttpRequestMessage(HttpMethod.Post, PostCommentUrl)
                { Content = JsonContent.Create(commentPayload) };
            req3.Headers.Add("Cookie", cookie);

            var res3 = await _httpClient.SendAsync(req3);
            var body3 = await res3.Content.ReadAsStringAsync();

            if (!res3.IsSuccessStatusCode)
            {
            }
            else
            {
            }
        }

        // ── Post comment cho Back Panel
        public async Task PostCommentBySerialWithLogAsync(string serial, string commentBody, JsonElement testLog)
        {
            // ── Cookie ────────────────────────────────────
            var cookie = await GetCookieAsync();
            if (cookie == null) return;

            // ── Step 1: serial → lotId ────────────────────
            var lotPayload = new
            {
                id = 18,
                jsonrpc = "2.0",
                method = "call",
                @params = new
                {
                    model = "stock.lot",
                    method = "web_search_read",
                    args = Array.Empty<object>(),
                    kwargs = new
                    {
                        limit = 1,
                        offset = 0,
                        order = "",
                        domain = new object[] { new object[] { "name", "ilike", serial } },
                        fields = new[] { "name", "product_id" }
                    }
                }
            };

            int lotId;
            using (var req1 = new HttpRequestMessage(HttpMethod.Post, StockLotUrl)
            { Content = JsonContent.Create(lotPayload) })
            {
                req1.Headers.Add("Cookie", cookie);
                var res1 = await _httpClient.SendAsync(req1);
                res1.EnsureSuccessStatusCode();

                using var doc1 = JsonDocument.Parse(await res1.Content.ReadAsStringAsync());
                if (!doc1.RootElement.TryGetProperty("result", out var r)
                    || !r.TryGetProperty("records", out var records)
                    || records.ValueKind != JsonValueKind.Array
                    || records.GetArrayLength() == 0)
                    throw new Exception($"Không tìm thấy lot nào với serial '{serial}' trong Odoo.");

                lotId = records[0].GetProperty("id").GetInt32();
            }

            // ── Step 2: lotId → woPageId ──────────────────
            var tracePayload = new
            {
                id = 23,
                jsonrpc = "2.0",
                method = "call",
                @params = new
                {
                    args = new object[]
                    {
                        new
                        {
                            lang = "vi_VN", tz = "Asia/Ho_Chi_Minh",
                            uid  = 2, allowed_company_ids = new[] { 1 },
                            active_id = lotId, model = "stock.lot",
                            ttype = (object?)null, auto_unfold = false, lot_name = (object?)null
                        }
                    },
                    model = "stock.traceability.report",
                    method = "get_html",
                    kwargs = new
                    {
                        context = new
                        {
                            lang = "vi_VN",
                            tz = "Asia/Ho_Chi_Minh",
                            uid = 2,
                            allowed_company_ids = new[] { 1 }
                        }
                    }
                }
            };

            int woPageId;
            using (var req2 = new HttpRequestMessage(HttpMethod.Post, StockTraceabilityUrl)
            { Content = JsonContent.Create(tracePayload) })
            {
                req2.Headers.Add("Cookie", cookie);
                var res2 = await _httpClient.SendAsync(req2);
                res2.EnsureSuccessStatusCode();

                using var doc2 = await JsonDocument.ParseAsync(await res2.Content.ReadAsStreamAsync());
                var html = doc2.RootElement
                    .GetProperty("result").GetProperty("html").GetString() ?? "";

                var match = Regex.Match(html,
                    @"data-active-id=""(\d+)""[^>]*?>\s*(NM/MO/\d+(?:-\d+)?)\s*<",
                    RegexOptions.IgnoreCase);

                if (!match.Success)
                    throw new Exception($"Không tìm thấy Work Order (NM/MO/...) trong traceability của serial '{serial}'. LotId={lotId}.");

                woPageId = int.Parse(match.Groups[1].Value);
            }

            // ── Step 3: Build body comment + TestLog ─────
            // Duyệt JsonElement để build bảng HTML — tránh vấn đề deserialize Dictionary<string,object>
            var logHtml = new System.Text.StringBuilder();
            logHtml.Append("<br/><table border=\"1\" cellpadding=\"4\" cellspacing=\"0\" style=\"border-collapse:collapse;font-size:12px\">");
            logHtml.Append("<tr><th>Field</th><th>Value</th></tr>");

            if (testLog.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in testLog.EnumerateObject())
                {
                    var key = System.Net.WebUtility.HtmlEncode(prop.Name);
                    var val = System.Net.WebUtility.HtmlEncode(prop.Value.ToString());
                    logHtml.Append($"<tr><td><b>{key}</b></td><td>{val}</td></tr>");
                }
            }

            logHtml.Append("</table>");

            var fullBody = $"{commentBody}{logHtml}";

            // ── Step 4: Post comment ──────────────────────
            var commentPayload = new
            {
                id = 13,
                jsonrpc = "2.0",
                method = "call",
                @params = new
                {
                    post_data = new
                    {
                        attachment_ids = Array.Empty<int>(),
                        attachment_tokens = Array.Empty<string>(),
                        body = fullBody,
                        message_type = "comment",
                        partner_ids = Array.Empty<int>(),
                        canned_response_ids = Array.Empty<int>(),
                        subtype_xmlid = "mail.mt_comment"
                    },
                    thread_id = woPageId,
                    thread_model = "mrp.production",
                    context = new
                    {
                        mail_post_autofollow = true,
                        lang = "vi_VN",
                        tz = "Asia/Ho_Chi_Minh",
                        uid = 2,
                        allowed_company_ids = new[] { 1 }
                    }
                }
            };

            using var req3 = new HttpRequestMessage(HttpMethod.Post, PostCommentUrl)
            { Content = JsonContent.Create(commentPayload) };
            req3.Headers.Add("Cookie", cookie);

            var res3 = await _httpClient.SendAsync(req3);
            var body3 = await res3.Content.ReadAsStringAsync();

            if (!res3.IsSuccessStatusCode)
                throw new Exception($"Odoo trả lỗi HTTP {(int)res3.StatusCode}: {body3}");

            // Kiểm tra lỗi trong JSON response của Odoo (error field)
            using var doc3 = JsonDocument.Parse(body3);
            if (doc3.RootElement.TryGetProperty("error", out var errEl))
                throw new Exception($"Odoo trả lỗi: {errEl.GetRawText()}");
        }


        // Post comment cho Middel Panel
        public async Task PostCommentByLotWithLogAsync(
    string lotNumber,
    string commentBody,
    JsonElement testLog)
        {
            var cookie = await GetCookieAsync();
            if (cookie == null) return;

            // ── Step 1: lotNumber → lotId ─────────────────────────────
            var lotPayload = new
            {
                id = 18,
                jsonrpc = "2.0",
                method = "call",
                @params = new
                {
                    model = "stock.lot",
                    method = "web_search_read",
                    args = Array.Empty<object>(),
                    kwargs = new
                    {
                        limit = 1,
                        offset = 0,
                        order = "",
                        domain = new object[] { new object[] { "name", "=", lotNumber } },
                        fields = new[] { "name" }
                    }
                }
            };

            int lotId;
            using (var req1 = new HttpRequestMessage(HttpMethod.Post, StockLotUrl)
            { Content = JsonContent.Create(lotPayload) })
            {
                req1.Headers.Add("Cookie", cookie);
                var res1 = await _httpClient.SendAsync(req1);
                res1.EnsureSuccessStatusCode();

                using var doc1 = JsonDocument.Parse(await res1.Content.ReadAsStringAsync());
                if (!doc1.RootElement.TryGetProperty("result", out var r)
                    || !r.TryGetProperty("records", out var records)
                    || records.ValueKind != JsonValueKind.Array
                    || records.GetArrayLength() == 0)
                    throw new Exception($"Không tìm thấy lot '{lotNumber}' trong Odoo.");

                lotId = records[0].GetProperty("id").GetInt32();
            }

            // ── Step 2: lotId → mrp.production id (woPageId) ──────────
            var moPayload = new
            {
                id = 19,
                jsonrpc = "2.0",
                method = "call",
                @params = new
                {
                    model = "mrp.production",
                    method = "web_search_read",
                    args = Array.Empty<object>(),
                    kwargs = new
                    {
                        limit = 1,
                        offset = 0,
                        order = "",
                        domain = new object[]
                        {
                    new object[] { "lot_producing_id", "=", lotId }
                        },
                        fields = new[] { "id", "name" }
                    }
                }
            };

            int woPageId;
            using (var req2 = new HttpRequestMessage(HttpMethod.Post,
                       "https://sigmaworldwide.io/web/dataset/call_kw/mrp.production/web_search_read")
            { Content = JsonContent.Create(moPayload) })
            {
                req2.Headers.Add("Cookie", cookie);
                var res2 = await _httpClient.SendAsync(req2);
                res2.EnsureSuccessStatusCode();

                using var doc2 = JsonDocument.Parse(await res2.Content.ReadAsStringAsync());
                if (!doc2.RootElement.TryGetProperty("result", out var r2)
                    || !r2.TryGetProperty("records", out var records2)
                    || records2.ValueKind != JsonValueKind.Array
                    || records2.GetArrayLength() == 0)
                    throw new Exception($"Không tìm thấy MO nào với lot_producing_id={lotId} (lot='{lotNumber}').");

                woPageId = records2[0].GetProperty("id").GetInt32();
            }

            // ── Step 3: Build comment + TestLog HTML ───────────────────
            var logHtml = new System.Text.StringBuilder();
            logHtml.Append("<br/><table border=\"1\" cellpadding=\"4\" cellspacing=\"0\" style=\"border-collapse:collapse;font-size:12px\">");
            logHtml.Append("<tr><th>Field</th><th>Value</th></tr>");
            if (testLog.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in testLog.EnumerateObject())
                {
                    var key = System.Net.WebUtility.HtmlEncode(prop.Name);
                    var val = System.Net.WebUtility.HtmlEncode(prop.Value.ToString());
                    logHtml.Append($"<tr><td><b>{key}</b></td><td>{val}</td></tr>");
                }
            }
            logHtml.Append("</table>");

            var fullBody = $"{commentBody}{logHtml}";

            // ── Step 4: Post comment ───────────────────────────────────
            var commentPayload = new
            {
                id = 13,
                jsonrpc = "2.0",
                method = "call",
                @params = new
                {
                    post_data = new
                    {
                        attachment_ids = Array.Empty<int>(),
                        attachment_tokens = Array.Empty<string>(),
                        body = fullBody,
                        message_type = "comment",
                        partner_ids = Array.Empty<int>(),
                        canned_response_ids = Array.Empty<int>(),
                        subtype_xmlid = "mail.mt_comment"
                    },
                    thread_id = woPageId,
                    thread_model = "mrp.production",
                    context = new
                    {
                        mail_post_autofollow = true,
                        lang = "vi_VN",
                        tz = "Asia/Ho_Chi_Minh",
                        uid = 2,
                        allowed_company_ids = new[] { 1 }
                    }
                }
            };

            using var req3 = new HttpRequestMessage(HttpMethod.Post, PostCommentUrl)
            { Content = JsonContent.Create(commentPayload) };
            req3.Headers.Add("Cookie", cookie);

            var res3 = await _httpClient.SendAsync(req3);
            var body3 = await res3.Content.ReadAsStringAsync();

            if (!res3.IsSuccessStatusCode)
                throw new Exception($"Odoo HTTP {(int)res3.StatusCode}: {body3}");

            using var doc3 = JsonDocument.Parse(body3);
            if (doc3.RootElement.TryGetProperty("error", out var errEl))
                throw new Exception($"Odoo error: {errEl.GetRawText()}");
        }

    }
}
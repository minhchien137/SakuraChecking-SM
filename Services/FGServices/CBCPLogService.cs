using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using ScanCheckSakura.Data;
using ScanCheckSakura.Models;

namespace ScanCheckSakura.Services.FGServices
{
    public class CBCPLogService : ICBCPLogService
    {
        private readonly ApplicationDbContext _db;

        public CBCPLogService(ApplicationDbContext db)
        {
            _db = db;
        }

        // ── Lấy log có lọc + phân trang ──────────────────────

        public async Task<CBCPLogViewModel> GetLogsAsync(CBCPLogFilter filter)
        {
            var query = _db.CBCPLogs.AsQueryable();

            // Lọc theo BoxSN
            if (!string.IsNullOrWhiteSpace(filter.BoxSN))
                query = query.Where(x => x.BoxSN.Contains(filter.BoxSN.Trim()));

            // Lọc theo ProductSN
            if (!string.IsNullOrWhiteSpace(filter.ProductSN))
                query = query.Where(x => x.ProductSN.Contains(filter.ProductSN.Trim()));

            // Lọc theo kết quả PASS/FAIL
            if (!string.IsNullOrWhiteSpace(filter.Result))
                query = query.Where(x => x.Result == filter.Result);

            // Lọc theo ngày
            if (filter.DateFrom.HasValue)
                query = query.Where(x => x.CheckedAt >= filter.DateFrom.Value.ToUniversalTime());

            if (filter.DateTo.HasValue)
                query = query.Where(x => x.CheckedAt <= filter.DateTo.Value
                                                                       .AddDays(1)
                                                                       .ToUniversalTime());

            // Đếm tổng
            var totalCount = await query.CountAsync();
            var passCount  = await query.CountAsync(x => x.Result == "PASS");
            var failCount  = await query.CountAsync(x => x.Result == "FAIL");

            // Phân trang
            var logs = await query
                .OrderByDescending(x => x.CheckedAt)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            return new CBCPLogViewModel
            {
                Logs       = logs,
                Filter     = filter,
                TotalCount = totalCount,
                PassCount  = passCount,
                FailCount  = failCount,
            };
        }

        // ── Xuất Excel ────────────────────────────────────────

        public async Task<byte[]> ExportExcelAsync(CBCPLogFilter filter)
        {
            // Lấy toàn bộ dữ liệu đã lọc (không phân trang khi xuất)
            var exportFilter = new CBCPLogFilter
            {
                BoxSN     = filter.BoxSN,
                ProductSN = filter.ProductSN,
                Result    = filter.Result,
                DateFrom  = filter.DateFrom,
                DateTo    = filter.DateTo,
                Page      = 1,
                PageSize  = int.MaxValue
            };

            var vm   = await GetLogsAsync(exportFilter);
            var logs = vm.Logs;

            using var workbook  = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("CBCP Log");

            // ── Style header row ──────────────────────────────
            var headers = new[]
            {
                "No.", "Thời Gian", "Color Box SN", "Product SN",
                "Màu Hộp", "Màu SP", "Kết Quả", "Ghi Chú"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = sheet.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold         = true;
                cell.Style.Font.FontColor    = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1a1f27");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#334155");
            }

            // ── Data rows ─────────────────────────────────────
            for (int i = 0; i < logs.Count; i++)
            {
                var log = logs[i];
                int row = i + 2;

                sheet.Cell(row, 1).Value = i + 1;
                sheet.Cell(row, 2).Value = log.CheckedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
                sheet.Cell(row, 3).Value = log.BoxSN;
                sheet.Cell(row, 4).Value = log.ProductSN;
                sheet.Cell(row, 5).Value = log.BoxColor     ?? "—";
                sheet.Cell(row, 6).Value = log.ProductColor ?? "—";
                sheet.Cell(row, 7).Value = log.Result;
                sheet.Cell(row, 8).Value = log.ErrorMessage ?? "—";

                // Màu nền xen kẽ
                var rowBg = i % 2 == 0
                    ? XLColor.FromHtml("#0f1217")
                    : XLColor.FromHtml("#1a1f27");

                for (int col = 1; col <= 8; col++)
                {
                    var cell = sheet.Cell(row, col);
                    cell.Style.Fill.BackgroundColor = rowBg;
                    cell.Style.Font.FontColor = XLColor.FromHtml("#e2e8f0");
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#2d3748");
                }

                // Tô màu cột Kết Quả
                var resultCell = sheet.Cell(row, 7);
                if (log.Result == "PASS")
                {
                    resultCell.Style.Font.FontColor    = XLColor.FromHtml("#22c55e");
                    resultCell.Style.Font.Bold         = true;
                }
                else
                {
                    resultCell.Style.Font.FontColor    = XLColor.FromHtml("#ef4444");
                    resultCell.Style.Font.Bold         = true;
                }
            }

            // ── Auto-fit column width ─────────────────────────
            sheet.Columns().AdjustToContents();
            sheet.Column(8).Width = 40; // Ghi chú rộng hơn

            // ── Freeze header row ─────────────────────────────
            sheet.SheetView.FreezeRows(1);

            // ── Ghi vào byte array ────────────────────────────
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
}

// ============================================================
// Controllers/FGCheckController.cs
// Thêm action CBCPCheck cho AJAX — trả về JSON
// ============================================================

using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ScanCheckSakura.Models;
using ScanCheckSakura.Services.FGServices;

namespace ScanCheckSakura.Controllers
{
    public class FGCheckController : Controller
    {
        private readonly ILogger<FGCheckController> _logger;
        private readonly ICBCPService    _cbcpService;
        private readonly ICBCPLogService _cbcpLogService;

        public FGCheckController(
            ILogger<FGCheckController> logger,
            ICBCPService    cbcpService,
            ICBCPLogService cbcpLogService)
        {
            _logger         = logger;
            _cbcpService    = cbcpService;
            _cbcpLogService = cbcpLogService;
        }

        // ── GET: trang chính ─────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> CBCP()
        {
            var vm = new CBCPViewModel
            {
                RecentLogs = await _cbcpService.GetRecentLogsAsync(50)
            };
            return View(vm);
        }

        // ── POST AJAX: /FGCheck/CBCPCheck ────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CBCPCheck([FromForm] string boxSN, [FromForm] string productSN)
        {
            if (string.IsNullOrWhiteSpace(boxSN) || string.IsNullOrWhiteSpace(productSN))
            {
                return Json(new
                {
                    isPass       = false,
                    errorMessage = "Vui lòng nhập đầy đủ Color Box SN và Product SN.",
                    boxColor     = (string?)null,
                    productColor = (string?)null,
                    workOrderInfo= (string?)null,
                    recentLogs   = Array.Empty<object>()
                });
            }

            _logger.LogInformation("CBCP AJAX — Box: {Box} | Product: {Prod}", boxSN, productSN);

            var result = _cbcpService.Check(boxSN, productSN);

            try { await _cbcpService.SaveLogAsync(boxSN, productSN, result); }
            catch (Exception ex) { _logger.LogError("SaveLog FAILED: {Msg}", ex.Message); }

            var logs = await _cbcpService.GetRecentLogsAsync(50);

            return Json(new
            {
                isPass       = result.IsPass,
                errorMessage = result.ErrorMessage,
                boxColor     = result.BoxColor,
                productColor = result.ProductColor,
                workOrderInfo= result.WorkOrderInfo,
                recentLogs   = logs.Select(l => new
                {
                    checkedAt    = l.CheckedAt.ToLocalTime().ToString("dd/MM HH:mm:ss"),
                    boxSN        = l.BoxSN,
                    productSN    = l.ProductSN,
                    boxColor     = l.BoxColor,
                    productColor = l.ProductColor,
                    result       = l.Result,
                    errorMessage = l.ErrorMessage
                })
            });
        }

        // ── CBCPLog – trang lịch sử ──────────────────────────
        [HttpGet]
        public async Task<IActionResult> CBCPLog(CBCPLogFilter filter)
        {
            var vm = await _cbcpLogService.GetLogsAsync(filter);
            return View(vm);
        }

        // ── Xuất Excel ────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> CBCPLogExport(CBCPLogFilter filter)
        {
            var bytes    = await _cbcpLogService.ExportExcelAsync(filter);
            var fileName = $"CBCP_Log_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        // ── Error ─────────────────────────────────────────────
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}

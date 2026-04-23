using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScanCheckSakura.Data;
using ScanCheckSakura.Models;
using ScanCheckSakura.Services;

public class FQCController : Controller
{
    private readonly IFqcbpService _fqcbpService;
    private readonly IFqcOdooService _fqcOdooService;

    private readonly ApplicationDbContext _db;

    public FQCController(IFqcbpService fqcbpService, IFqcOdooService fqcOdooService, ApplicationDbContext db)
    {
        _fqcbpService = fqcbpService;
        _fqcOdooService = fqcOdooService;
        _db = db;
    }

    public IActionResult FQCBP() => View();

    [HttpGet]
    public async Task<IActionResult> qty([FromQuery] string workOrder)
    {
        if (string.IsNullOrWhiteSpace(workOrder))
            return BadRequest(new { message = "workOrder is required" });

        var (qty, passQty, ngQty) = await _fqcbpService.GetQtyAsync(workOrder.Trim().ToUpper());
        return Json(new { qty, passQty, ngQty });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> scan([FromBody] ScanRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.WorkOrder))
            return BadRequest(new { message = "WorkOrder is required" });
        if (string.IsNullOrWhiteSpace(req.SerialNumber) || req.SerialNumber.Trim().Length != 15)
            return BadRequest(new { message = "SerialNumber must be exactly 15 characters" });
        if (req.Status != "PASS" && req.Status != "NG")
            return BadRequest(new { message = "Status must be PASS or NG" });

        var wo = req.WorkOrder.Trim().ToUpper();
        var sn = req.SerialNumber.Trim().ToUpper();

        // 1. Lưu DB
        var (qty, passQty, ngQty) = await _fqcbpService.RecordScanAsync(wo, sn, req.Status);

        // 2. Gửi comment Odoo
        try
        {
            var commentBody = $"@FQC FQC : {req.Status}";
            await _fqcOdooService.PostCommentBySerialAsync(sn, commentBody);
        }
        catch (Exception ex)
        {
        }

        return Json(new { qty, passQty, ngQty });
    }

    [HttpGet]
    public async Task<IActionResult> checkSN([FromQuery] string sn, [FromQuery] string wo)
    {
        if (string.IsNullOrWhiteSpace(sn))
            return BadRequest(new { message = "该序列号已被扫描" });

        sn = sn.Trim().ToUpper();
        wo = wo?.Trim().ToUpper() ?? "";

        // Check chỉ trong cùng Work Order
        var exists = await _db.SM_FQCBP_H
            .AnyAsync(x => x.SerialNumber == sn && x.WorkOrder == wo);

        return Json(new { isDuplicate = exists });
    }

    [HttpGet]
    public async Task<IActionResult> FQCBPH(FQCBPLogFilter filter)
    {
        filter.Page = Math.Max(1, filter.Page);
        filter.PageSize = filter.PageSize > 0 ? filter.PageSize : 50;

        // Query base
        var query = _db.SM_FQCBP_H.AsQueryable();

        // Filters
        if (!string.IsNullOrWhiteSpace(filter.WorkOrder))
            query = query.Where(x => x.WorkOrder.Contains(filter.WorkOrder.Trim().ToUpper()));

        if (!string.IsNullOrWhiteSpace(filter.SerialNumber))
            query = query.Where(x => x.SerialNumber.Contains(filter.SerialNumber.Trim().ToUpper()));

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(x => x.Status == filter.Status);

        if (filter.DateFrom.HasValue)
            query = query.Where(x => x.Timeline >= filter.DateFrom.Value.Date);

        if (filter.DateTo.HasValue)
            query = query.Where(x => x.Timeline < filter.DateTo.Value.Date.AddDays(1));

        // Counts
        var totalCount = await query.CountAsync();
        var passCount = await query.CountAsync(x => x.Status == "PASS");
        var ngCount = await query.CountAsync(x => x.Status == "NG");
        var totalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize);

        // Paged data — mới nhất trước
        var logs = await query
            .OrderByDescending(x => x.Timeline)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        var vm = new FQCBPLogViewModel
        {
            Logs = logs,
            Filter = filter,
            TotalCount = totalCount,
            PassCount = passCount,
            NgCount = ngCount,
            CurrentPage = filter.Page,
            TotalPages = totalPages
        };

        return View(vm);
    }

    /* /FQC/FQCBPExport — xuất CSV với timezone UTC+8 */
    [HttpGet]
    public async Task<IActionResult> FQCBPExport(FQCBPLogFilter filter)
    {
        var query = _db.SM_FQCBP_H.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.WorkOrder))
            query = query.Where(x => x.WorkOrder.Contains(filter.WorkOrder.Trim().ToUpper()));

        if (!string.IsNullOrWhiteSpace(filter.SerialNumber))
            query = query.Where(x => x.SerialNumber.Contains(filter.SerialNumber.Trim().ToUpper()));

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(x => x.Status == filter.Status);

        if (filter.DateFrom.HasValue)
            query = query.Where(x => x.Timeline >= filter.DateFrom.Value.Date);

        if (filter.DateTo.HasValue)
            query = query.Where(x => x.Timeline < filter.DateTo.Value.Date.AddDays(1));

        var data = await query.OrderByDescending(x => x.Timeline).ToListAsync();

        // Build CSV — Server UTC+7, hiển thị UTC+8 (Trung Quốc) → +1 giờ
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("#,Work Order,Serial Number,Status,Time (UTC+8)");
        int i = 1;
        foreach (var r in data)
            sb.AppendLine($"{i++},{r.WorkOrder},{r.SerialNumber},{r.Status},{r.Timeline.AddHours(1):dd/MM/yyyy HH:mm:ss}");

        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString()))
            .ToArray();

        var fileName = $"FQC_History_{DateTime.Now.AddHours(1):yyyyMMdd_HHmmss}.csv";
        return File(bytes, "text/csv", fileName);
    }

    // ── Trạm A: chỉ comment, không có TestLog ────────────────
    [HttpPost]
    public async Task<IActionResult> SendOdooComment([FromBody] OdooCommentRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.SerialNumber))
            return BadRequest(new { success = false, message = "SerialNumber is required" });
        if (string.IsNullOrWhiteSpace(req.CommentBody))
            return BadRequest(new { success = false, message = "CommentBody is required" });

        var sn = req.SerialNumber.Trim().ToUpper();
        var body = req.CommentBody.Trim();

        try
        {
            await _fqcOdooService.PostCommentBySerialAsync(sn, body);

            return Ok(new
            {
                success = true,
                message = "Comment sent successfully",
                detail = new { serialNumber = sn, commentBody = body }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Failed to send comment to Odoo",
                error = ex.Message,
                detail = new
                {
                    serialNumber = sn,
                    commentBody = body,
                    exceptionType = ex.GetType().Name,
                    stackTrace = ex.StackTrace
                }
            });
        }
    }

    [HttpGet]
    public async Task<IActionResult> getWOFromSerial([FromQuery] string serial)
    {
        if (string.IsNullOrWhiteSpace(serial))
            return BadRequest(new { message = "serial is required" });

        var sn = serial.Trim().ToUpper();

        var record = await _db.SVN_ProductionInputLogs
            .Where(x => x.SerialCode != null && x.SerialCode.ToUpper() == sn)
            .OrderByDescending(x => x.DateFinished)
            .Select(x => x.MasterWoCode)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(record))
            return NotFound(new { message = $"Serial '{sn}' not found" });

        return Ok(new { workOrder = record });
    }


    // ── Trạm B: comment + TestLog ─────────────────────────────
    [HttpPost]
    public async Task<IActionResult> SendOdooCommentBackPanel([FromBody] OdooCommentV2Request req)
    {
        if (string.IsNullOrWhiteSpace(req.SerialNumber))
            return BadRequest(new { success = false, message = "SerialNumber is required" });
        if (string.IsNullOrWhiteSpace(req.CommentBody))
            return BadRequest(new { success = false, message = "CommentBody is required" });

        var sn = req.SerialNumber.Trim().ToUpper();
        var body = req.CommentBody.Trim();

        try
        {
            await _fqcOdooService.PostCommentBySerialWithLogAsync(sn, body, req.TestLog);

            return Ok(new
            {
                success = true,
                message = "Comment sent successfully",
                detail = new { serialNumber = sn, commentBody = body }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                success = false,
                message = "Failed to send comment to Odoo",
                error = ex.Message,
                detail = new
                {
                    serialNumber = sn,
                    commentBody = body,
                    exceptionType = ex.GetType().Name,
                    stackTrace = ex.StackTrace
                }
            });
        }
    }

    // ── Request models ────────────────────────────────────────
    public class OdooCommentRequest
    {
        public string SerialNumber { get; set; } = string.Empty;
        public string CommentBody { get; set; } = string.Empty;
    }

    public class OdooCommentV2Request
    {
        public string SerialNumber { get; set; } = string.Empty;
        public string CommentBody { get; set; } = string.Empty;
        public System.Text.Json.JsonElement TestLog { get; set; }
    }

}



public class ScanRequest
{
    public string WorkOrder { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
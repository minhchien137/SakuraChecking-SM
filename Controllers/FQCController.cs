using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScanCheckSakura.Data;
using ScanCheckSakura.Models;
using ScanCheckSakura.Services;

public class FQCController : Controller
{
    private readonly IFqcbpService   _fqcbpService;
    private readonly IFqcOdooService _fqcOdooService;
    private readonly ApplicationDbContext _db;

    public FQCController(IFqcbpService fqcbpService, IFqcOdooService fqcOdooService, ApplicationDbContext db)
    {
        _fqcbpService   = fqcbpService;
        _fqcOdooService = fqcOdooService;
        _db             = db;
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

    // ── GET /FQC/ngReasons ────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> ngReasons()
    {
        var reasons = await _db.SVN_quality_reason
            .Where(x => x.operation != null && x.operation.TrimEnd().EndsWith("(SM)"))
            .OrderBy(x => x.priority)
            .ThenBy(x => x.name)
            .Select(x => new { x.id, x.name, x.code })
            .ToListAsync();

        return Json(reasons);
    }

    // ── POST /FQC/scan ────────────────────────────────────
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

        var (qty, passQty, ngQty) = await _fqcbpService.RecordScanAsync(
            wo, sn, req.Status,
            req.NgCode?.Trim(),
            req.NgReason?.Trim(),
            req.NgDescription?.Trim());

        try
        {
            var commentBody = $"@FQC FQC : {req.Status}";
            await _fqcOdooService.PostCommentBySerialAsync(sn, commentBody);
        }
        catch { }

        return Json(new { qty, passQty, ngQty });
    }

    [HttpGet]
    public async Task<IActionResult> checkSN([FromQuery] string sn, [FromQuery] string wo)
    {
        if (string.IsNullOrWhiteSpace(sn))
            return BadRequest(new { message = "sn is required" });

        sn = sn.Trim().ToUpper();
        wo = wo?.Trim().ToUpper() ?? "";

        var exists = await _db.SM_FQCBP_H.AnyAsync(x => x.SerialNumber == sn && x.WorkOrder == wo);
        return Json(new { isDuplicate = exists });
    }

    // ── GET /FQC/FQCBPH ───────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> FQCBPH(FQCBPLogFilter filter)
    {
        filter.Page     = Math.Max(1, filter.Page);
        filter.PageSize = filter.PageSize > 0 ? filter.PageSize : 50;

        var query = _db.SM_FQCBP_H.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.WorkOrder))
            query = query.Where(x => x.WorkOrder.Contains(filter.WorkOrder.Trim().ToUpper()));
        if (!string.IsNullOrWhiteSpace(filter.SerialNumber))
            query = query.Where(x => x.SerialNumber.Contains(filter.SerialNumber.Trim().ToUpper()));
        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(x => x.Status == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.NgCode))                                       // ← NEW
            query = query.Where(x => x.NgCode != null &&
                                     x.NgCode.Contains(filter.NgCode.Trim().ToUpper()));     // ← NEW
        if (filter.DateFrom.HasValue)
            query = query.Where(x => x.Timeline >= filter.DateFrom.Value.Date);
        if (filter.DateTo.HasValue)
            query = query.Where(x => x.Timeline < filter.DateTo.Value.Date.AddDays(1));

        var totalCount = await query.CountAsync();
        var passCount  = await query.CountAsync(x => x.Status == "PASS");
        var ngCount    = await query.CountAsync(x => x.Status == "NG");
        var totalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize);

        var logs = await query
            .OrderByDescending(x => x.Timeline)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return View(new FQCBPLogViewModel
        {
            Logs        = logs,
            Filter      = filter,
            TotalCount  = totalCount,
            PassCount   = passCount,
            NgCount     = ngCount,
            CurrentPage = filter.Page,
            TotalPages  = totalPages
        });
    }

    // ── GET /FQC/FQCBPExport ──────────────────────────────
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
        if (!string.IsNullOrWhiteSpace(filter.NgCode))                                        // ← NEW
            query = query.Where(x => x.NgCode != null &&
                                     x.NgCode.Contains(filter.NgCode.Trim().ToUpper()));      // ← NEW
        if (filter.DateFrom.HasValue)
            query = query.Where(x => x.Timeline >= filter.DateFrom.Value.Date);
        if (filter.DateTo.HasValue)
            query = query.Where(x => x.Timeline < filter.DateTo.Value.Date.AddDays(1));

        var data = await query.OrderByDescending(x => x.Timeline).ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("#,Work Order,Serial Number,Status,NG Code,NG Reason,NG Description,Time (UTC+8)");
        int i = 1;
        foreach (var r in data)
        {
            var desc = r.NgDescription?.Replace(",", ";") ?? "";
            var reason = r.NgReason?.Replace(",", ";") ?? "";
            sb.AppendLine($"{i++},{r.WorkOrder},{r.SerialNumber},{r.Status},{r.NgCode ?? ""},{reason},{desc},{r.Timeline.AddHours(1):dd/MM/yyyy HH:mm:ss}");
        }

        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString()))
            .ToArray();

        return File(bytes, "text/csv", $"FQC_History_{DateTime.Now.AddHours(1):yyyyMMdd_HHmmss}.csv");
    }

    [HttpGet]
    public IActionResult FQCBPR() => View();

    //FQC/reportData  
    [HttpGet]
    public async Task<IActionResult> reportData([FromQuery] FQCReportFilter filter)
    {
        var query = _db.SM_FQCBP_H.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.WorkOrder))
            query = query.Where(x => x.WorkOrder.Contains(filter.WorkOrder.Trim().ToUpper()));
        if (!string.IsNullOrWhiteSpace(filter.NgCode))
            query = query.Where(x => x.NgCode != null &&
                                     x.NgCode.Contains(filter.NgCode.Trim().ToUpper()));
        if (filter.DateFrom.HasValue)
            query = query.Where(x => x.Timeline >= filter.DateFrom.Value.Date);
        if (filter.DateTo.HasValue)
            query = query.Where(x => x.Timeline < filter.DateTo.Value.Date.AddDays(1));

        // ── Load tất cả rows (có SerialNumber và NgDescription cho detail table) ──
        var rows = await query
            .OrderByDescending(x => x.Timeline)
            .Select(x => new
            {
                x.WorkOrder,
                x.SerialNumber,
                x.Status,
                x.NgCode,
                x.NgReason,
                x.NgDescription,
                x.Timeline
            })
            .ToListAsync();

        // ── KPI ──────────────────────────────────────────
        int totalPass = rows.Count(x => x.Status == "PASS");
        int totalNg = rows.Count(x => x.Status == "NG");

        // ── Daily trend ───────────────────────────────────
        var dailyTrend = rows
            .GroupBy(x => x.Timeline.Date)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                date = g.Key.ToString("yyyy-MM-dd"),
                pass = g.Count(x => x.Status == "PASS"),
                ng = g.Count(x => x.Status == "NG")
            })
            .ToList();

        // ── NG per Work Order ─────────────────────────────
        var ngPerWo = rows
            .GroupBy(x => x.WorkOrder)
            .Select(g => new
            {
                workOrder = g.Key,
                ng = g.Count(x => x.Status == "NG"),
                pass = g.Count(x => x.Status == "PASS"),
                total = g.Count()
            })
            .OrderByDescending(x => x.ng)
            .Take(20)
            .ToList();

        // ── NG per Code (pareto) ──────────────────────────
        var ngPerCode = rows
            .Where(x => x.Status == "NG")
            .GroupBy(x => new { Code = x.NgCode ?? "", Reason = x.NgReason ?? "" })
            .Select(g => new { code = g.Key.Code, reason = g.Key.Reason, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToList();

        // ── NG trend per WO (top 5) ───────────────────────
        var top5Wos = ngPerWo.Take(5).Select(x => x.workOrder).ToHashSet();
        var ngTrendPerWo = rows
            .Where(x => x.Status == "NG" && top5Wos.Contains(x.WorkOrder))
            .GroupBy(x => x.WorkOrder)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(x => x.Timeline.Date)
                       .OrderBy(d => d.Key)
                       .Select(d => new { date = d.Key.ToString("yyyy-MM-dd"), ng = d.Count() })
                       .ToList<object>());

        // ── NG code breakdown per WO (top 8) ─────────────
        var top8Wos = ngPerWo.Take(8).Select(x => x.workOrder).ToHashSet();
        var ngCodePerWo = rows
            .Where(x => x.Status == "NG" && top8Wos.Contains(x.WorkOrder))
            .GroupBy(x => x.WorkOrder)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(x => x.NgCode ?? "Unknown")
                       .ToDictionary(cg => cg.Key, cg => cg.Count()));

        // ── Heatmap: hour × day-of-week (Mon=0…Sun=6) ────
        var heatmap = new Dictionary<int, Dictionary<int, int>>();
        foreach (var r in rows.Where(x => x.Status == "NG"))
        {
            int hour = r.Timeline.Hour;
            int dow = ((int)r.Timeline.DayOfWeek + 6) % 7; // Mon=0
            if (!heatmap.ContainsKey(hour)) heatmap[hour] = new();
            heatmap[hour].TryGetValue(dow, out int cur);
            heatmap[hour][dow] = cur + 1;
        }

        // ── NG Detail rows (chỉ lấy NG, tối đa 2000 records) ──────────
        var ngDetailRows = rows
            .Where(x => x.Status == "NG")
            .Take(2000)
            .Select(x => new
            {
                workOrder = x.WorkOrder,
                serialNumber = x.SerialNumber,
                status = x.Status,
                ngCode = x.NgCode,
                ngReason = x.NgReason,
                ngDescription = x.NgDescription,
                timeline = x.Timeline
            })
            .ToList();

        return Json(new
        {
            totalScanned = rows.Count,
            totalPass,
            totalNg,
            uniqueWo = rows.Select(x => x.WorkOrder).Distinct().Count(),
            uniqueNgCode = ngPerCode.Count,
            dailyTrend,
            ngPerWo,
            ngPerCode,
            ngTrendPerWo,
            ngCodePerWo,
            heatmap,
            ngDetailRows    // ← MỚI: bảng detail NG
        });
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

 

    [HttpPost]
    public async Task<IActionResult> SendOdooComment([FromBody] OdooCommentRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.SerialNumber))
            return BadRequest(new { message = "SerialNumber is required" });
        if (string.IsNullOrWhiteSpace(req.CommentBody))
            return BadRequest(new { message = "CommentBody is required" });

        await _fqcOdooService.PostCommentBySerialAsync(
            req.SerialNumber.Trim().ToUpper(), req.CommentBody.Trim());

        return Ok(new { message = "Comment sent" });
    }

    public class OdooCommentRequest
    {
        public string SerialNumber { get; set; } = string.Empty;
        public string CommentBody  { get; set; } = string.Empty;
    }
}

public class ScanRequest
{
    public string  WorkOrder     { get; set; } = string.Empty;
    public string  SerialNumber  { get; set; } = string.Empty;
    public string  Status        { get; set; } = string.Empty;
    public string? NgCode        { get; set; }
    public string? NgReason      { get; set; }
    public string? NgDescription { get; set; }
}

public class FQCReportFilter
{
    public string?   WorkOrder { get; set; }
    public string?   NgCode    { get; set; }
    public DateTime? DateFrom  { get; set; }
    public DateTime? DateTo    { get; set; }
}
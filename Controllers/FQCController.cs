using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ScanCheckSakura.Data;
using ScanCheckSakura.Models;
using ScanCheckSakura.Services;

public class FQCController : Controller
{
    private readonly IFqcbpService      _fqcbpService;
    private readonly IFqcfpyService     _fqcfpyService;
    private readonly IFqcOdooService    _fqcOdooService;
    private readonly IDefectSyncService _defectSyncService;
    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;

    public FQCController(
        IFqcbpService      fqcbpService,
        IFqcfpyService     fqcfpyService,
        IFqcOdooService    fqcOdooService,
        IDefectSyncService defectSyncService,
        ApplicationDbContext db,
        IHttpClientFactory httpClientFactory)
    {
        _fqcbpService      = fqcbpService;
        _fqcfpyService     = fqcfpyService;
        _fqcOdooService    = fqcOdooService;
        _defectSyncService = defectSyncService;
        _db                = db;
        _httpClientFactory = httpClientFactory;
    }

    public IActionResult FQCBP() => View();

    // ── Trạm FQC bán thành phẩm (semi-finished product) ────
    // Cùng dùng chung view/scan pipeline với FQCBP, chỉ khác Station.
    // Bắt buộc SN phải đã input tại MiddlePanel + BackPanel trước khi được check ở đây.
    public IActionResult FQC02() => View();
    public IActionResult FQC04() => View();

    // ── Trạm FPY (First Pass Yield) ─────────────────────────
    // Bảng riêng (SM_FQCFPY / SM_FQCFPY_H) — không dùng chung với FQCBP/FQC02/FQC04.
    // KHÔNG kiểm tra trùng Serial Number: cho phép quét lại cùng 1 SN nhiều lần.
    public IActionResult FPY() => View();

    [HttpGet]
    public async Task<IActionResult> qty([FromQuery] string workOrder, [FromQuery] string station = "FQCBP")
    {
        if (string.IsNullOrWhiteSpace(workOrder))
            return BadRequest(new { message = "workOrder is required" });

        var (qty, passQty, ngQty) = await _fqcbpService.GetQtyAsync(workOrder.Trim().ToUpper(), station.Trim().ToUpper());
        return Json(new { qty, passQty, ngQty });
    }

    // ── GET /FQC/checkStation ─────────────────────────────
    // Kiểm tra nhanh (dùng cho UI) xem SN đã đủ điều kiện input tại trạm này chưa,
    // gọi ngay sau khi quét SN, TRƯỚC khi cho nhập Status — chặn sớm, không cho vào trạm.
    [HttpGet]
    public async Task<IActionResult> checkStation([FromQuery] string sn, [FromQuery] string station)
    {
        if (string.IsNullOrWhiteSpace(sn))
            return BadRequest(new { message = "sn is required" });
        if (string.IsNullOrWhiteSpace(station))
            return BadRequest(new { message = "station is required" });

        var missing = await _fqcbpService.GetMissingPriorStationsAsync(sn.Trim().ToUpper(), station.Trim().ToUpper());
        return Json(new { blocked = missing.Count > 0, missingStations = missing });
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

    // ── Helper: gọi /api/Odoo/search để lấy color theo WO ──
    private async Task<string?> FetchColorFromOdooAsync(string workOrder)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var pathBase = Request.PathBase.Value?.TrimEnd('/') ?? "";
            var url = $"{Request.Scheme}://{Request.Host}{pathBase}/api/Odoo/search?productionCode={Uri.EscapeDataString(workOrder)}";
            var res = await client.GetAsync(url);
            if (!res.IsSuccessStatusCode) return null;
            var json = await res.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("productColor", out var c))
                return c.GetString();
        }
        catch { }
        return null;
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
        var station = string.IsNullOrWhiteSpace(req.Station) ? "FQCBP" : req.Station.Trim().ToUpper();

        // ── Station control: chặn ghi nhận nếu SN chưa input tại các trạm bắt buộc
        // (vd. FQC02/FQC04 yêu cầu SN đã có ở MiddlePanel + BackPanel) ──
        var missingStations = await _fqcbpService.GetMissingPriorStationsAsync(sn, station);
        if (missingStations.Count > 0)
        {
            return BadRequest(new
            {
                message = $"SN chưa được input tại: {string.Join(", ", missingStations)}",
                missingStations
            });
        }

        // ── Lấy color từ Odoo (nếu frontend không truyền lên) ──
        var color = string.IsNullOrWhiteSpace(req.Color)
            ? await FetchColorFromOdooAsync(wo)
            : req.Color.Trim();

        var (qty, passQty, ngQty) = await _fqcbpService.RecordScanAsync(
            wo, sn, req.Status, station,
            color,
            req.NgCode?.Trim(),
            req.NgReason?.Trim(),
            req.NgDescription?.Trim());

        try
        {
            var commentBody = $"@FQC FQC : {req.Status}";
            await _fqcOdooService.PostCommentBySerialAsync(sn, commentBody);
        }
        catch { }

        // Đồng bộ Defect Record khi scan NG
        if (req.Status == "NG" && !string.IsNullOrWhiteSpace(req.NgCode))
        {
            try
            {
                await _defectSyncService.UpsertDefectAsync(wo, sn, req.NgCode.Trim());
            }
            catch { }
        }

        return Json(new { qty, passQty, ngQty });
    }

    // ── GET /FQC/fpyQty ───────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> fpyQty([FromQuery] string workOrder)
    {
        if (string.IsNullOrWhiteSpace(workOrder))
            return BadRequest(new { message = "workOrder is required" });

        var (qty, passQty, ngQty) = await _fqcfpyService.GetQtyAsync(workOrder.Trim().ToUpper());
        return Json(new { qty, passQty, ngQty });
    }

    // ── POST /FQC/fpyScan ─────────────────────────────────
    // Không kiểm tra trùng Serial Number — FPY cho phép quét lại cùng 1 SN nhiều lần.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> fpyScan([FromBody] ScanRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.WorkOrder))
            return BadRequest(new { message = "WorkOrder is required" });
        if (string.IsNullOrWhiteSpace(req.SerialNumber) || req.SerialNumber.Trim().Length != 15)
            return BadRequest(new { message = "SerialNumber must be exactly 15 characters" });
        if (req.Status != "PASS" && req.Status != "NG")
            return BadRequest(new { message = "Status must be PASS or NG" });

        var wo = req.WorkOrder.Trim().ToUpper();
        var sn = req.SerialNumber.Trim().ToUpper();

        var color = string.IsNullOrWhiteSpace(req.Color)
            ? await FetchColorFromOdooAsync(wo)
            : req.Color.Trim();

        var (qty, passQty, ngQty) = await _fqcfpyService.RecordScanAsync(
            wo, sn, req.Status,
            color,
            req.NgCode?.Trim(),
            req.NgReason?.Trim(),
            req.NgDescription?.Trim());

        try
        {
            var commentBody = $"@FQC FQC : {req.Status}";
            await _fqcOdooService.PostCommentBySerialAsync(sn, commentBody);
        }
        catch { }

        if (req.Status == "NG" && !string.IsNullOrWhiteSpace(req.NgCode))
        {
            try
            {
                await _defectSyncService.UpsertDefectAsync(wo, sn, req.NgCode.Trim());
            }
            catch { }
        }

        return Json(new { qty, passQty, ngQty });
    }

    [HttpGet]
    public async Task<IActionResult> checkSN([FromQuery] string sn, [FromQuery] string wo, [FromQuery] string station = "FQCBP")
    {
        if (string.IsNullOrWhiteSpace(sn))
            return BadRequest(new { message = "sn is required" });

        sn = sn.Trim().ToUpper();
        wo = wo?.Trim().ToUpper() ?? "";
        station = string.IsNullOrWhiteSpace(station) ? "FQCBP" : station.Trim().ToUpper();

        var exists = await _db.SM_FQCBP_H.AnyAsync(x => x.SerialNumber == sn && x.WorkOrder == wo && x.Station == station);
        return Json(new { isDuplicate = exists });
    }

    // ── Shared query/view-model builder cho các trang History (dùng chung bởi
    //    FQCBPH và 2 trang "locked" theo trạm FQC02H/FQC04H) ──
    private IQueryable<SM_FQCBP_H> BuildHistoryQuery(FQCBPLogFilter filter, string? lockedStation)
    {
        var query = _db.SM_FQCBP_H.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.WorkOrder))
            query = query.Where(x => x.WorkOrder.Contains(filter.WorkOrder.Trim().ToUpper()));
        if (!string.IsNullOrWhiteSpace(filter.SerialNumber))
            query = query.Where(x => x.SerialNumber.Contains(filter.SerialNumber.Trim().ToUpper()));

        var station = lockedStation ?? filter.Station;
        if (!string.IsNullOrWhiteSpace(station))
            query = query.Where(x => x.Station == station.Trim().ToUpper());

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(x => x.Status == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.NgCode))
            query = query.Where(x => x.NgCode != null &&
                                     x.NgCode.Contains(filter.NgCode.Trim().ToUpper()));
        if (!string.IsNullOrWhiteSpace(filter.Color))
            query = query.Where(x => x.Color != null &&
                                     x.Color == filter.Color.Trim());
        if (filter.DateFrom.HasValue)
            query = query.Where(x => x.Timeline >= filter.DateFrom.Value.Date);
        if (filter.DateTo.HasValue)
            query = query.Where(x => x.Timeline < filter.DateTo.Value.Date.AddDays(1));

        return query;
    }

    private async Task<FQCBPLogViewModel> BuildHistoryViewModelAsync(FQCBPLogFilter filter, string? lockedStation = null)
    {
        filter.Page     = Math.Max(1, filter.Page);
        filter.PageSize = filter.PageSize > 0 ? filter.PageSize : 50;
        if (lockedStation != null) filter.Station = lockedStation;

        var query = BuildHistoryQuery(filter, lockedStation);

        var totalCount = await query.CountAsync();
        var passCount  = await query.CountAsync(x => x.Status == "PASS");
        var ngCount    = await query.CountAsync(x => x.Status == "NG");
        var totalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize);

        var logs = await query
            .OrderByDescending(x => x.Timeline)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return new FQCBPLogViewModel
        {
            Logs        = logs,
            Filter      = filter,
            TotalCount  = totalCount,
            PassCount   = passCount,
            NgCount     = ngCount,
            CurrentPage = filter.Page,
            TotalPages  = totalPages
        };
    }

    private async Task<IActionResult> ExportHistoryCsvAsync(FQCBPLogFilter filter, string? lockedStation = null)
    {
        var query = BuildHistoryQuery(filter, lockedStation);
        var data  = await query.OrderByDescending(x => x.Timeline).ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("#,Work Order,Station,Serial Number,Status,Color,NG Code,NG Reason,NG Description,Time (UTC+8)");
        int i = 1;
        foreach (var r in data)
        {
            var desc   = r.NgDescription?.Replace(",", ";") ?? "";
            var reason = r.NgReason?.Replace(",", ";") ?? "";
            sb.AppendLine($"{i++},{r.WorkOrder},{r.Station},{r.SerialNumber},{r.Status},{r.Color ?? ""},{r.NgCode ?? ""},{reason},{desc},{r.Timeline.AddHours(1):dd/MM/yyyy HH:mm:ss}");
        }

        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString()))
            .ToArray();

        return File(bytes, "text/csv", $"FQC_History_{DateTime.Now.AddHours(1):yyyyMMdd_HHmmss}.csv");
    }

    // ── GET /FQC/FQCBPH — lịch sử tổng (mọi trạm, có thể lọc theo Station) ──
    [HttpGet]
    public async Task<IActionResult> FQCBPH(FQCBPLogFilter filter)
        => View(await BuildHistoryViewModelAsync(filter));

    // ── GET /FQC/FQCBPExport ──────────────────────────────
    [HttpGet]
    public async Task<IActionResult> FQCBPExport(FQCBPLogFilter filter)
        => await ExportHistoryCsvAsync(filter);

    // ── GET /FQC/FQC02H — lịch sử khoá cứng theo trạm FQC02 ──
    [HttpGet]
    public async Task<IActionResult> FQC02H(FQCBPLogFilter filter)
        => View(await BuildHistoryViewModelAsync(filter, "FQC02"));

    [HttpGet]
    public async Task<IActionResult> FQC02HExport(FQCBPLogFilter filter)
        => await ExportHistoryCsvAsync(filter, "FQC02");

    // ── GET /FQC/FQC04H — lịch sử khoá cứng theo trạm FQC04 ──
    [HttpGet]
    public async Task<IActionResult> FQC04H(FQCBPLogFilter filter)
        => View(await BuildHistoryViewModelAsync(filter, "FQC04"));

    [HttpGet]
    public async Task<IActionResult> FQC04HExport(FQCBPLogFilter filter)
        => await ExportHistoryCsvAsync(filter, "FQC04");

    // ── Shared query/view-model builder cho trang History FPY ──
    private IQueryable<SM_FQCFPY_H> BuildFpyHistoryQuery(FQCFPYLogFilter filter)
    {
        var query = _db.SM_FQCFPY_H.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.WorkOrder))
            query = query.Where(x => x.WorkOrder.Contains(filter.WorkOrder.Trim().ToUpper()));
        if (!string.IsNullOrWhiteSpace(filter.SerialNumber))
            query = query.Where(x => x.SerialNumber.Contains(filter.SerialNumber.Trim().ToUpper()));
        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(x => x.Status == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.NgCode))
            query = query.Where(x => x.NgCode != null &&
                                     x.NgCode.Contains(filter.NgCode.Trim().ToUpper()));
        if (!string.IsNullOrWhiteSpace(filter.Color))
            query = query.Where(x => x.Color != null &&
                                     x.Color == filter.Color.Trim());
        if (filter.DateFrom.HasValue)
            query = query.Where(x => x.Timeline >= filter.DateFrom.Value.Date);
        if (filter.DateTo.HasValue)
            query = query.Where(x => x.Timeline < filter.DateTo.Value.Date.AddDays(1));

        return query;
    }

    private async Task<FQCFPYLogViewModel> BuildFpyHistoryViewModelAsync(FQCFPYLogFilter filter)
    {
        filter.Page     = Math.Max(1, filter.Page);
        filter.PageSize = filter.PageSize > 0 ? filter.PageSize : 50;

        var query = BuildFpyHistoryQuery(filter);

        var totalCount = await query.CountAsync();
        var passCount  = await query.CountAsync(x => x.Status == "PASS");
        var ngCount    = await query.CountAsync(x => x.Status == "NG");
        var totalPages = (int)Math.Ceiling(totalCount / (double)filter.PageSize);

        var logs = await query
            .OrderByDescending(x => x.Timeline)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();

        return new FQCFPYLogViewModel
        {
            Logs        = logs,
            Filter      = filter,
            TotalCount  = totalCount,
            PassCount   = passCount,
            NgCount     = ngCount,
            CurrentPage = filter.Page,
            TotalPages  = totalPages
        };
    }

    private async Task<IActionResult> ExportFpyHistoryCsvAsync(FQCFPYLogFilter filter)
    {
        var query = BuildFpyHistoryQuery(filter);
        var data  = await query.OrderByDescending(x => x.Timeline).ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("#,Work Order,Station,Serial Number,Status,Color,NG Code,NG Reason,NG Description,Time (UTC+8)");
        int i = 1;
        foreach (var r in data)
        {
            var desc   = r.NgDescription?.Replace(",", ";") ?? "";
            var reason = r.NgReason?.Replace(",", ";") ?? "";
            sb.AppendLine($"{i++},{r.WorkOrder},{r.Station},{r.SerialNumber},{r.Status},{r.Color ?? ""},{r.NgCode ?? ""},{reason},{desc},{r.Timeline.AddHours(1):dd/MM/yyyy HH:mm:ss}");
        }

        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString()))
            .ToArray();

        return File(bytes, "text/csv", $"FPY_History_{DateTime.Now.AddHours(1):yyyyMMdd_HHmmss}.csv");
    }

    // ── GET /FQC/FPYH — lịch sử trạm FPY ──────────────────
    [HttpGet]
    public async Task<IActionResult> FPYH(FQCFPYLogFilter filter)
        => View(await BuildFpyHistoryViewModelAsync(filter));

    // ── GET /FQC/FPYExport ─────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> FPYExport(FQCFPYLogFilter filter)
        => await ExportFpyHistoryCsvAsync(filter);

    [HttpGet]
    public IActionResult FPYR() => View();

    // ── GET /FQC/fpyReportData ─────────────────────────────
    [HttpGet]
    public async Task<IActionResult> fpyReportData([FromQuery] FQCFPYReportFilter filter)
    {
        var query = _db.SM_FQCFPY_H.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.WorkOrder))
            query = query.Where(x => x.WorkOrder.Contains(filter.WorkOrder.Trim().ToUpper()));
        if (!string.IsNullOrWhiteSpace(filter.NgCode))
            query = query.Where(x => x.NgCode != null &&
                                     x.NgCode.Contains(filter.NgCode.Trim().ToUpper()));
        if (!string.IsNullOrWhiteSpace(filter.Color))
            query = query.Where(x => x.Color != null &&
                                     x.Color == filter.Color.Trim());
        if (filter.DateFrom.HasValue)
            query = query.Where(x => x.Timeline >= filter.DateFrom.Value.Date);
        if (filter.DateTo.HasValue)
            query = query.Where(x => x.Timeline < filter.DateTo.Value.Date.AddDays(1));

        var allRows = await query
            .OrderBy(x => x.Timeline)
            .Select(x => new
            {
                x.WorkOrder,
                x.SerialNumber,
                x.Status,
                x.Color,
                x.NgCode,
                x.NgReason,
                x.NgDescription,
                x.Timeline
            })
            .ToListAsync();

        // First Pass Yield: mỗi Serial Number chỉ tính 1 lần duy nhất — lấy kết quả
        // của LƯỢT QUÉT ĐẦU TIÊN (Timeline sớm nhất). Các lượt quét lại cùng SN (rework)
        // vẫn nằm đầy đủ trong SM_FQCFPY_H / trang History, chỉ không tính vào KPI/biểu đồ report này.
        var rows = allRows
            .GroupBy(x => x.SerialNumber)
            .Select(g => g.First())
            .OrderByDescending(x => x.Timeline)
            .ToList();

        int totalPass = rows.Count(x => x.Status == "PASS");
        int totalNg   = rows.Count(x => x.Status == "NG");

        var dailyTrend = rows
            .GroupBy(x => x.Timeline.Date)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                date = g.Key.ToString("yyyy-MM-dd"),
                pass = g.Count(x => x.Status == "PASS"),
                ng   = g.Count(x => x.Status == "NG")
            })
            .ToList();

        var ngPerWo = rows
            .GroupBy(x => x.WorkOrder)
            .Select(g => new
            {
                workOrder = g.Key,
                ng    = g.Count(x => x.Status == "NG"),
                pass  = g.Count(x => x.Status == "PASS"),
                total = g.Count()
            })
            .OrderByDescending(x => x.ng)
            .Take(20)
            .ToList();

        var ngPerCode = rows
            .Where(x => x.Status == "NG")
            .GroupBy(x => new { Code = x.NgCode ?? "", Reason = x.NgReason ?? "" })
            .Select(g => new { code = g.Key.Code, reason = g.Key.Reason, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToList();

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

        var top8Wos = ngPerWo.Take(8).Select(x => x.workOrder).ToHashSet();
        var ngCodePerWo = rows
            .Where(x => x.Status == "NG" && top8Wos.Contains(x.WorkOrder))
            .GroupBy(x => x.WorkOrder)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(x => x.NgCode ?? "Unknown")
                       .ToDictionary(cg => cg.Key, cg => cg.Count()));

        var heatmap = new Dictionary<int, Dictionary<int, int>>();
        foreach (var r in rows.Where(x => x.Status == "NG"))
        {
            int hour = r.Timeline.Hour;
            int dow  = ((int)r.Timeline.DayOfWeek + 6) % 7;
            if (!heatmap.ContainsKey(hour)) heatmap[hour] = new();
            heatmap[hour].TryGetValue(dow, out int cur);
            heatmap[hour][dow] = cur + 1;
        }

        var ngDetailRows = rows
            .Where(x => x.Status == "NG")
            .Take(2000)
            .Select(x => new
            {
                workOrder     = x.WorkOrder,
                serialNumber  = x.SerialNumber,
                status        = x.Status,
                color         = x.Color,
                ngCode        = x.NgCode,
                ngReason      = x.NgReason,
                ngDescription = x.NgDescription,
                timeline      = x.Timeline
            })
            .ToList();

        var availableColors = rows
            .Where(x => !string.IsNullOrWhiteSpace(x.Color))
            .Select(x => x.Color!)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        return Json(new
        {
            totalScanned = rows.Count,
            totalPass,
            totalNg,
            uniqueWo     = rows.Select(x => x.WorkOrder).Distinct().Count(),
            uniqueNgCode = ngPerCode.Count,
            dailyTrend,
            ngPerWo,
            ngPerCode,
            ngTrendPerWo,
            ngCodePerWo,
            heatmap,
            ngDetailRows,
            availableColors
        });
    }

    [HttpGet]
    public IActionResult FQCBPR() => View();

    // ── GET /FQC/reportData ───────────────────────────────
    [HttpGet]
    public async Task<IActionResult> reportData([FromQuery] FQCReportFilter filter)
    {
        var query = _db.SM_FQCBP_H.AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.WorkOrder))
            query = query.Where(x => x.WorkOrder.Contains(filter.WorkOrder.Trim().ToUpper()));
        if (!string.IsNullOrWhiteSpace(filter.Station))
            query = query.Where(x => x.Station == filter.Station.Trim().ToUpper());
        if (!string.IsNullOrWhiteSpace(filter.NgCode))
            query = query.Where(x => x.NgCode != null &&
                                     x.NgCode.Contains(filter.NgCode.Trim().ToUpper()));
        // ── Filter by Color ──────────────────────────────
        if (!string.IsNullOrWhiteSpace(filter.Color))
            query = query.Where(x => x.Color != null &&
                                     x.Color == filter.Color.Trim());
        if (filter.DateFrom.HasValue)
            query = query.Where(x => x.Timeline >= filter.DateFrom.Value.Date);
        if (filter.DateTo.HasValue)
            query = query.Where(x => x.Timeline < filter.DateTo.Value.Date.AddDays(1));

        // ── Load tất cả rows ────────────────────────────
        var rows = await query
            .OrderByDescending(x => x.Timeline)
            .Select(x => new
            {
                x.WorkOrder,
                x.SerialNumber,
                x.Status,
                x.Color,        // ← NEW
                x.NgCode,
                x.NgReason,
                x.NgDescription,
                x.Timeline
            })
            .ToListAsync();

        // ── KPI ──────────────────────────────────────────
        int totalPass = rows.Count(x => x.Status == "PASS");
        int totalNg   = rows.Count(x => x.Status == "NG");

        // ── Daily trend ───────────────────────────────────
        var dailyTrend = rows
            .GroupBy(x => x.Timeline.Date)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                date = g.Key.ToString("yyyy-MM-dd"),
                pass = g.Count(x => x.Status == "PASS"),
                ng   = g.Count(x => x.Status == "NG")
            })
            .ToList();

        // ── NG per Work Order ─────────────────────────────
        var ngPerWo = rows
            .GroupBy(x => x.WorkOrder)
            .Select(g => new
            {
                workOrder = g.Key,
                ng    = g.Count(x => x.Status == "NG"),
                pass  = g.Count(x => x.Status == "PASS"),
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
            int dow  = ((int)r.Timeline.DayOfWeek + 6) % 7;
            if (!heatmap.ContainsKey(hour)) heatmap[hour] = new();
            heatmap[hour].TryGetValue(dow, out int cur);
            heatmap[hour][dow] = cur + 1;
        }

        // ── NG Detail rows (tối đa 2000 records) ──────────
        var ngDetailRows = rows
            .Where(x => x.Status == "NG")
            .Take(2000)
            .Select(x => new
            {
                workOrder     = x.WorkOrder,
                serialNumber  = x.SerialNumber,
                status        = x.Status,
                color         = x.Color,        // ← NEW
                ngCode        = x.NgCode,
                ngReason      = x.NgReason,
                ngDescription = x.NgDescription,
                timeline      = x.Timeline
            })
            .ToList();

        // ── Danh sách màu duy nhất để populate dropdown ──
        var availableColors = rows
            .Where(x => !string.IsNullOrWhiteSpace(x.Color))
            .Select(x => x.Color!)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        return Json(new
        {
            totalScanned = rows.Count,
            totalPass,
            totalNg,
            uniqueWo     = rows.Select(x => x.WorkOrder).Distinct().Count(),
            uniqueNgCode = ngPerCode.Count,
            dailyTrend,
            ngPerWo,
            ngPerCode,
            ngTrendPerWo,
            ngCodePerWo,
            heatmap,
            ngDetailRows,
            availableColors    // ← NEW: dùng để populate dropdown filter
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

    // ── GET /FQC/getColorFromSerial?serial=RM15A0050500001 ────
    // Test endpoint: xác định màu sản phẩm cục bộ từ Serial Number,
    // KHÔNG gọi Odoo. Cùng logic với getColorFromSerial() trong FQCBP.cshtml.
    // Format: RM15A + Variant(2) + Year(1) + Day(3) + Sequential(4)
    //         01234    56           7          89A      BCDE
    private static readonly Dictionary<string, string> _serialVariantColorMap = new()
    {
        { "00", "Blue"   },
        { "01", "Pink"   },
        { "02", "Green"  },
        { "03", "Luther" },
    };

    [HttpGet]
    public IActionResult getColorFromSerial([FromQuery] string serial)
    {
        if (string.IsNullOrWhiteSpace(serial))
            return BadRequest(new { message = "serial is required" });

        var sn = serial.Trim().ToUpper();
        if (sn.Length < 7)
            return Ok(new { serial = sn, variantCode = (string?)null, color = (string?)null });

        var variantCode = sn.Substring(5, 2);
        _serialVariantColorMap.TryGetValue(variantCode, out var color);

        return Ok(new { serial = sn, variantCode, color });
    }

    // ── POST /FQC/backfillDefect?date=20260617 ────────────────
    // Backfill Item_code cho data cũ rồi sync vào SVN_Defect_Record
    [HttpPost]
    public async Task<IActionResult> backfillDefect([FromQuery] string date)
    {
        if (string.IsNullOrWhiteSpace(date))
            return BadRequest(new { message = "Thiếu tham số date (yyyyMMdd). VD: ?date=20260617" });

        var result = await _defectSyncService.BackfillAndSyncAsync(date.Trim());
        return Json(result);
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
    public string? Station       { get; set; }   // "FQCBP" (mặc định) | "FQC02" | "FQC04"
    public string? Color         { get; set; }   // ← NEW (frontend có thể truyền lên để tránh gọi thêm)
    public string? NgCode        { get; set; }
    public string? NgReason      { get; set; }
    public string? NgDescription { get; set; }
}

public class FQCReportFilter
{
    public string?   WorkOrder { get; set; }
    public string?   Station   { get; set; }
    public string?   NgCode    { get; set; }
    public string?   Color     { get; set; }   // ← NEW
    public DateTime? DateFrom  { get; set; }
    public DateTime? DateTo    { get; set; }
}

public class FQCFPYReportFilter
{
    public string?   WorkOrder { get; set; }
    public string?   NgCode    { get; set; }
    public string?   Color     { get; set; }
    public DateTime? DateFrom  { get; set; }
    public DateTime? DateTo    { get; set; }
}
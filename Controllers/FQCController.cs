using Microsoft.AspNetCore.Mvc;
using ScanCheckSakura.Services;

public class FQCController : Controller
{
    private readonly IFqcbpService _fqcbpService;
    private readonly IFqcOdooService _fqcOdooService;

    public FQCController(IFqcbpService fqcbpService, IFqcOdooService fqcOdooService)
    {
        _fqcbpService   = fqcbpService;
        _fqcOdooService = fqcOdooService;
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

        // 2. Gửi comment Odoo — await trực tiếp để debug rõ lỗi
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
}

public class ScanRequest
{
    public string WorkOrder    { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string Status       { get; set; } = string.Empty;
}
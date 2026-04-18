// ════════════════════════════════════════════════════════════════════════
// FILE: FQCReportViewModel.cs
// Thêm vào namespace ScanCheckSakura.Models
// ════════════════════════════════════════════════════════════════════════

namespace ScanCheckSakura.Models
{
    public class FQCReportFilter
    {
        public string?   WorkOrder { get; set; }
        public string?   NgCode    { get; set; }
        public DateTime? DateFrom  { get; set; }
        public DateTime? DateTo    { get; set; }
    }

    public class FQCReportViewModel
    {
        // ── KPI ──────────────────────────────────────────────
        public int TotalScanned { get; set; }
        public int TotalPass    { get; set; }
        public int TotalNg      { get; set; }
        public int UniqueWo     { get; set; }
        public int UniqueNgCode { get; set; }

        // ── Daily trend [{date, pass, ng}] ───────────────────
        public List<DailyPoint> DailyTrend { get; set; } = new();

        // ── NG per Work Order [{workOrder, ng, pass, total}] ─
        public List<WoNgItem> NgPerWo { get; set; } = new();

        // ── NG per Code [{code, reason, count}] ──────────────
        public List<NgCodeItem> NgPerCode { get; set; } = new();

        // ── NG trend per WO {workOrder: [{date, ng}]} ────────
        public Dictionary<string, List<DailyNgPoint>> NgTrendPerWo { get; set; } = new();

        // ── NG code breakdown per WO {wo: {code: count}} ─────
        public Dictionary<string, Dictionary<string, int>> NgCodePerWo { get; set; } = new();

        // ── Heatmap {hour(0-23): {dow(0=Mon..6=Sun): count}} ─
        public Dictionary<int, Dictionary<int, int>> Heatmap { get; set; } = new();
    }

    public record DailyPoint(string Date, int Pass, int Ng);
    public record WoNgItem(string WorkOrder, int Ng, int Pass, int Total);
    public record NgCodeItem(string Code, string Reason, int Count);
    public record DailyNgPoint(string Date, int Ng);
}


   

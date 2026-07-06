namespace ScanCheckSakura.Models
{
    public class FQCBPLogFilter
    {
        public string? WorkOrder    { get; set; }
        public string? SerialNumber { get; set; }
        public string? Status       { get; set; }
        public string? NgCode { get; set; }   // ← NEW
        public string? Color { get; set; }
        public DateTime? DateFrom   { get; set; }
        public DateTime? DateTo     { get; set; }
        public int Page     { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class FQCBPLogViewModel
    {
        public List<SM_FQCBP_H> Logs { get; set; } = new();
        public FQCBPLogFilter Filter      { get; set; } = new();
        public int TotalCount  { get; set; }
        public int PassCount   { get; set; }
        public int NgCount     { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages  { get; set; }
    }
}

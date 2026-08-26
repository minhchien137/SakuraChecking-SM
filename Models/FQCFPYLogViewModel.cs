namespace ScanCheckSakura.Models
{
    public class FQCFPYLogFilter
    {
        public string? WorkOrder    { get; set; }
        public string? SerialNumber { get; set; }
        public string? Status       { get; set; }
        public string? NgCode { get; set; }
        public string? Color { get; set; }
        public DateTime? DateFrom   { get; set; }
        public DateTime? DateTo     { get; set; }
        public int Page     { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class FQCFPYLogViewModel
    {
        public List<SM_FQCFPY_H> Logs { get; set; } = new();
        public FQCFPYLogFilter Filter      { get; set; } = new();
        public int TotalCount  { get; set; }
        public int PassCount   { get; set; }
        public int NgCount     { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages  { get; set; }
    }
}

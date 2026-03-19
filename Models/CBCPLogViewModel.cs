namespace ScanCheckSakura.Models
{

    // Bộ lọc tìm kiếm

    public class CBCPLogFilter
    {
        // Tìm theo mã
        public string? BoxSN { get; set; }
        public string? ProductSN { get; set; }

        // Lọc theo kết quả
        public string? Result { get; set; }   // "PASS", "FAIL", hoặc null = tất cả

        // Lọc theo ngày
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo   { get; set; }

        // Phân trang
        public int Page     { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }


    // ViewModel cho trang CBCPLog

    public class CBCPLogViewModel
    {
        public List<SM_CBCPLog> Logs       { get; set; } = new();
        public CBCPLogFilter    Filter     { get; set; } = new();

        // Phân trang
        public int TotalCount  { get; set; }
        public int TotalPages  => (int)Math.Ceiling((double)TotalCount / Filter.PageSize);
        public int CurrentPage => Filter.Page;

        // Thống kê nhanh
        public int PassCount   { get; set; }
        public int FailCount   { get; set; }
    }
}

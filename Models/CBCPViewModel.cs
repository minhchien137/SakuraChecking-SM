namespace ScanCheckSakura.Models
{
    public class CBCPViewModel
    {
        public InputModel Input { get; set; } = new InputModel();
        public CheckResult? LastResult { get; set; }


        public List<SM_CBCPLog> RecentLogs { get; set; } = new();

        public class InputModel
        {
            public string? BoxSN { get; set; }
            public string? ProductSN { get; set; }
        }

        public class CheckResult
        {
            public bool IsPass { get; set; }
            public string? BoxColor { get; set; }
            public string? ProductColor { get; set; }
            public string? WorkOrderInfo { get; set; }
            public string? ErrorMessage { get; set; }
        }
    }
}
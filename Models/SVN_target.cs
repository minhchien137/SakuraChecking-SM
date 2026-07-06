using System.ComponentModel.DataAnnotations.Schema;

namespace ScanCheckSakura.Models
{
    [Table("SVN_target")]
    public class SVN_target
    {
        public string? Operation { get; set; }
        public decimal? Daily_plan { get; set; }
        public decimal? UPH { get; set; }
        public decimal? UPPH { get; set; }
        public decimal? Labor { get; set; }
        public decimal? Defect { get; set; }
        public string? Date_time { get; set; }
        public decimal? Workingtime { get; set; }
        public string? Shift { get; set; }
    }
}
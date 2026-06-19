using System.ComponentModel.DataAnnotations.Schema;

namespace ScanCheckSakura.Models
{
    [Table("SVN_Defect_Record")]
    public class SVN_Defect_Record
    {
        public string? Item_code { get; set; }
        public string? Defect_Code { get; set; }
        public int?    Qty_NG { get; set; }
        public string? INSDatetime { get; set; }
        public string? Operation { get; set; }
        public string? Employer_code { get; set; }
        public string? Employer_name { get; set; }
    }
}
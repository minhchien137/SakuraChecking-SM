using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScanCheckSakura.Models
{
    [Table("SM_FQCBP")]
    public class SM_FQCBP
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string WorkOrder { get; set; } = string.Empty;

        // Rollup theo (WorkOrder, Station) — mỗi trạm có số liệu Qty/Pass/NG riêng
        [Required]
        [MaxLength(20)]
        public string Station { get; set; } = "FQCBP";

        public int Qty     { get; set; } = 0;
        public int PassQty { get; set; } = 0;
        public int NgQty   { get; set; } = 0;
    }
}
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScanCheckSakura.Models
{
    [Table("SM_FQCFPY")]
    public class SM_FQCFPY
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string WorkOrder { get; set; } = string.Empty;

        // Rollup theo WorkOrder cho trạm FPY (First Pass Yield)
        [Required]
        [MaxLength(20)]
        public string Station { get; set; } = "FPY";

        public int Qty     { get; set; } = 0;
        public int PassQty { get; set; } = 0;
        public int NgQty   { get; set; } = 0;
    }
}

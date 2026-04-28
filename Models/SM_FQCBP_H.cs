using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScanCheckSakura.Models
{
    [Table("SM_FQCBP_H")]
    public class SM_FQCBP_H
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string WorkOrder { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string SerialNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(10)]
        public string Status { get; set; } = string.Empty; // "PASS" | "NG"

        public DateTime Timeline { get; set; } = DateTime.Now;

        [MaxLength(50)]
        public string? Color { get; set; }

        // ── NG detail (nullable — chỉ dùng khi Status == "NG") ──

        [MaxLength(50)]
        public string? NgCode { get; set; }

        [MaxLength(200)]
        public string? NgReason { get; set; }

        [MaxLength(500)]
        public string? NgDescription { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScanCheckSakura.Models
{
    [Table("SM_FQCFPY_H")]
    public class SM_FQCFPY_H
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string WorkOrder { get; set; } = string.Empty;

        // Trạm thực hiện scan: luôn là "FPY"
        [Required]
        [MaxLength(20)]
        public string Station { get; set; } = "FPY";

        // Không có ràng buộc duy nhất trên SerialNumber — FPY cho phép quét lại
        // cùng 1 SN nhiều lần, mỗi lần quét là 1 record riêng.
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

        // Item code lấy từ Odoo sau khi scan NG (dùng để sync SVN_Defect_Record)
        [MaxLength(200)]
        public string? Item_code { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScanCheckSakura.Models
{
    [Table("SM_CBCPLog")]
    public class SM_CBCPLog
    {
        [Key]
        public int Id { get; set; }

    
        [MaxLength(20)]
        public string BoxSN { get; set; } = string.Empty;

     
        [MaxLength(20)]
        public string ProductSN { get; set; } = string.Empty;

      
        [MaxLength(20)]
        public string? BoxColor { get; set; }

    
        [MaxLength(20)]
        public string? ProductColor { get; set; }

     
        [MaxLength(10)]
        public string Result { get; set; } = string.Empty;

      
        [MaxLength(300)]
        public string? ErrorMessage { get; set; }

   
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    }
}
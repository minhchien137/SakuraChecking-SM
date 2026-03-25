using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScanCheckSakura.Models
{
    public class SM_Defect_Cookie
    {
        [Key]
        [Column("id")]
        public int id { get; set; }

        [Column("cookie")]
        [StringLength(300)]
        public string cookie { get; set; }
    }
}
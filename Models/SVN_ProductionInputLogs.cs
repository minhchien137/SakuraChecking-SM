using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ScanCheckSakura.Models
{
    [Table("SVN_ProductionInputLogs")]
    public class SVN_ProductionInputLogs
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        [Column("level")]
        public int? Level { get; set; }

        [Column("product_id")]
        public int? ProductId { get; set; }

        [Column("product_qty")]
        public decimal? ProductQty { get; set; }

        [Column("date_finished")]
        public DateTime? DateFinished { get; set; }

        // nvarchar(MAX) — không cần MaxLength
        [Column("product_type")]
        public string? ProductType { get; set; }

        [Column("serial_code")]
        public string? SerialCode { get; set; }

        [Column("component_list")]
        public string? ComponentList { get; set; }

        [Column("state")]
        public string? State { get; set; }

        [Column("API_function")]
        public string? ApiFunction { get; set; }

        [Column("API_parameters")]
        public string? ApiParameters { get; set; }

        [Column("status")]
        public string? Status { get; set; }

        [Column("wo_code")]
        public string? WoCode { get; set; }

        [Column("master_wo_code")]
        public string? MasterWoCode { get; set; }

        [Column("total_qty")]
        public decimal? TotalQty { get; set; }

        [Column("remain_qty")]
        public decimal? RemainQty { get; set; }

        [Column("consumed_wo_code")]
        public string? ConsumedWoCode { get; set; }
    }
}
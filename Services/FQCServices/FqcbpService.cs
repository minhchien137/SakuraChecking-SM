using Microsoft.EntityFrameworkCore;
using ScanCheckSakura.Data;
using ScanCheckSakura.Models;

namespace ScanCheckSakura.Services
{
    public interface IFqcbpService
    {
        Task<(int qty, int passQty, int ngQty)> GetQtyAsync(string workOrder, string station);

        Task<(int qty, int passQty, int ngQty)> RecordScanAsync(
            string workOrder, string serialNumber, string status, string station,
            string? color = null,
            string? ngCode = null, string? ngReason = null, string? ngDescription = null);

        // Trả về danh sách tên trạm bắt buộc mà SN CHƯA được input, dùng để chặn
        // scan tại các trạm FQC bán thành phẩm (FQC02/FQC04) khi chưa qua MiddlePanel/BackPanel.
        Task<List<string>> GetMissingPriorStationsAsync(string serialNumber, string station);
    }

    public class FqcbpService : IFqcbpService
    {
        private readonly ApplicationDbContext _db;

        public FqcbpService(ApplicationDbContext db) => _db = db;

        // Cấu hình: trạm nào bắt buộc phải có input tại các trạm nào trước đó.
        // TODO: xác nhận giá trị thật của SVN_ProductionInputLogs.ProductType / State
        // (đang lấy theo Odoo MES convention: state == "done" là đã hoàn thành)
        // ứng với MiddlePanel/BackPanel rồi chỉnh lại điều kiện lọc bên dưới cho khớp.
        private static readonly Dictionary<string, string[]> _requiredPriorStations =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["FQC02"] = new[] { "MiddlePanel", "BackPanel" },
                ["FQC04"] = new[] { "MiddlePanel", "BackPanel" },
            };

        public async Task<List<string>> GetMissingPriorStationsAsync(string serialNumber, string station)
        {
            var missing = new List<string>();
            if (!_requiredPriorStations.TryGetValue(station, out var requiredStations))
                return missing; // trạm này không bị quản lý bởi station-control

            var sn = serialNumber.Trim().ToUpper();

            foreach (var priorStation in requiredStations)
            {
                bool inputted = await _db.SVN_ProductionInputLogs.AnyAsync(x =>
                    x.SerialCode != null && x.SerialCode.ToUpper() == sn &&
                    x.ProductType != null && x.ProductType.ToUpper().Contains(priorStation.ToUpper()) &&
                    x.State != null && x.State.ToUpper() == "DONE");

                if (!inputted)
                    missing.Add(priorStation);
            }

            return missing;
        }

        public async Task<(int qty, int passQty, int ngQty)> GetQtyAsync(string workOrder, string station)
        {
            var record = await _db.SM_FQCBP.FirstOrDefaultAsync(x => x.WorkOrder == workOrder && x.Station == station);
            if (record == null) return (0, 0, 0);
            return (record.Qty, record.PassQty, record.NgQty);
        }

        public async Task<(int qty, int passQty, int ngQty)> RecordScanAsync(
            string workOrder, string serialNumber, string status, string station,
            string? color = null,
            string? ngCode = null, string? ngReason = null, string? ngDescription = null)
        {
            var history = new SM_FQCBP_H
            {
                WorkOrder     = workOrder,
                Station       = station,
                SerialNumber  = serialNumber,
                Status        = status,
                Timeline      = DateTime.Now,
                Color         = string.IsNullOrWhiteSpace(color) ? null : color.Trim(),
                NgCode        = status == "NG" ? ngCode        : null,
                NgReason      = status == "NG" ? ngReason      : null,
                NgDescription = status == "NG" ? ngDescription : null
            };
            _db.SM_FQCBP_H.Add(history);

            var summary = await _db.SM_FQCBP.FirstOrDefaultAsync(x => x.WorkOrder == workOrder && x.Station == station);
            if (summary == null)
            {
                summary = new SM_FQCBP
                {
                    WorkOrder = workOrder,
                    Station   = station,
                    Qty       = 1,
                    PassQty   = status == "PASS" ? 1 : 0,
                    NgQty     = status == "NG"   ? 1 : 0
                };
                _db.SM_FQCBP.Add(summary);
            }
            else
            {
                summary.Qty++;
                if (status == "PASS") summary.PassQty++;
                else                  summary.NgQty++;
            }

            await _db.SaveChangesAsync();
            return (summary.Qty, summary.PassQty, summary.NgQty);
        }
    }
}

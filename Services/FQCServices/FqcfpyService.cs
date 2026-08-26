using Microsoft.EntityFrameworkCore;
using ScanCheckSakura.Data;
using ScanCheckSakura.Models;

namespace ScanCheckSakura.Services
{
    public interface IFqcfpyService
    {
        Task<(int qty, int passQty, int ngQty)> GetQtyAsync(string workOrder);

        Task<(int qty, int passQty, int ngQty)> RecordScanAsync(
            string workOrder, string serialNumber, string status,
            string? color = null,
            string? ngCode = null, string? ngReason = null, string? ngDescription = null);
    }

    // Trạm FPY (First Pass Yield) — bảng riêng SM_FQCFPY / SM_FQCFPY_H.
    // Khác với FqcbpService: KHÔNG kiểm tra trùng Serial Number, cho phép quét lại
    // cùng 1 SN nhiều lần và mỗi lần quét đều được lưu thành 1 record lịch sử riêng.
    public class FqcfpyService : IFqcfpyService
    {
        private const string Station = "FPY";

        private readonly ApplicationDbContext _db;

        public FqcfpyService(ApplicationDbContext db) => _db = db;

        public async Task<(int qty, int passQty, int ngQty)> GetQtyAsync(string workOrder)
        {
            var record = await _db.SM_FQCFPY.FirstOrDefaultAsync(x => x.WorkOrder == workOrder && x.Station == Station);
            if (record == null) return (0, 0, 0);
            return (record.Qty, record.PassQty, record.NgQty);
        }

        public async Task<(int qty, int passQty, int ngQty)> RecordScanAsync(
            string workOrder, string serialNumber, string status,
            string? color = null,
            string? ngCode = null, string? ngReason = null, string? ngDescription = null)
        {
            var history = new SM_FQCFPY_H
            {
                WorkOrder     = workOrder,
                Station       = Station,
                SerialNumber  = serialNumber,
                Status        = status,
                Timeline      = DateTime.Now,
                Color         = string.IsNullOrWhiteSpace(color) ? null : color.Trim(),
                NgCode        = status == "NG" ? ngCode        : null,
                NgReason      = status == "NG" ? ngReason      : null,
                NgDescription = status == "NG" ? ngDescription : null
            };
            _db.SM_FQCFPY_H.Add(history);

            var summary = await _db.SM_FQCFPY.FirstOrDefaultAsync(x => x.WorkOrder == workOrder && x.Station == Station);
            if (summary == null)
            {
                summary = new SM_FQCFPY
                {
                    WorkOrder = workOrder,
                    Station   = Station,
                    Qty       = 1,
                    PassQty   = status == "PASS" ? 1 : 0,
                    NgQty     = status == "NG"   ? 1 : 0
                };
                _db.SM_FQCFPY.Add(summary);
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

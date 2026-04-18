using Microsoft.EntityFrameworkCore;
using ScanCheckSakura.Data;
using ScanCheckSakura.Models;

namespace ScanCheckSakura.Services
{
    public interface IFqcbpService
    {
        Task<(int qty, int passQty, int ngQty)> GetQtyAsync(string workOrder);

        Task<(int qty, int passQty, int ngQty)> RecordScanAsync(
            string workOrder, string serialNumber, string status,
            string? ngCode = null, string? ngReason = null, string? ngDescription = null);
    }

    public class FqcbpService : IFqcbpService
    {
        private readonly ApplicationDbContext _db;

        public FqcbpService(ApplicationDbContext db) => _db = db;

        public async Task<(int qty, int passQty, int ngQty)> GetQtyAsync(string workOrder)
        {
            var record = await _db.SM_FQCBP_Dev.FirstOrDefaultAsync(x => x.WorkOrder == workOrder);
            if (record == null) return (0, 0, 0);
            return (record.Qty, record.PassQty, record.NgQty);
        }

        public async Task<(int qty, int passQty, int ngQty)> RecordScanAsync(
            string workOrder, string serialNumber, string status,
            string? ngCode = null, string? ngReason = null, string? ngDescription = null)
        {
            var history = new SM_FQCBP_H_Dev
            {
                WorkOrder     = workOrder,
                SerialNumber  = serialNumber,
                Status        = status,
                Timeline      = DateTime.Now,
                NgCode        = status == "NG" ? ngCode        : null,
                NgReason      = status == "NG" ? ngReason      : null,
                NgDescription = status == "NG" ? ngDescription : null
            };
            _db.SM_FQCBP_H_Dev.Add(history);

            var summary = await _db.SM_FQCBP_Dev.FirstOrDefaultAsync(x => x.WorkOrder == workOrder);
            if (summary == null)
            {
                summary = new SM_FQCBP_Dev
                {
                    WorkOrder = workOrder,
                    Qty       = 1,
                    PassQty   = status == "PASS" ? 1 : 0,
                    NgQty     = status == "NG"   ? 1 : 0
                };
                _db.SM_FQCBP_Dev.Add(summary);
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
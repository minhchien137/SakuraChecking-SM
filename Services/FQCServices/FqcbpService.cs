using Microsoft.EntityFrameworkCore;
using ScanCheckSakura.Data;
using ScanCheckSakura.Models;

namespace ScanCheckSakura.Services
{
    public interface IFqcbpService
    {
        /// <summary>Lấy Qty, PassQty, NgQty hiện tại của WO.</summary>
        Task<(int qty, int passQty, int ngQty)> GetQtyAsync(string workOrder);

        /// <summary>Ghi 1 lần quét, trả về (Qty, PassQty, NgQty) mới nhất.</summary>
        Task<(int qty, int passQty, int ngQty)> RecordScanAsync(string workOrder, string serialNumber, string status);
    }

    public class FqcbpService : IFqcbpService
    {
        private readonly ApplicationDbContext _db;

        public FqcbpService(ApplicationDbContext db)
        {
            _db = db;
        }

        // ── GetQty ────────────────────────────────────────
        public async Task<(int qty, int passQty, int ngQty)> GetQtyAsync(string workOrder)
        {
            var record = await _db.SM_FQCBP
                .FirstOrDefaultAsync(x => x.WorkOrder == workOrder);

            if (record == null) return (0, 0, 0);
            return (record.Qty, record.PassQty, record.NgQty);
        }

        // ── RecordScan ────────────────────────────────────
        public async Task<(int qty, int passQty, int ngQty)> RecordScanAsync(string workOrder, string serialNumber, string status)
        {
            // 1. Ghi lịch sử
            var history = new SM_FQCBP_H
            {
                WorkOrder    = workOrder,
                SerialNumber = serialNumber,
                Status       = status,
                Timeline     = DateTime.Now
            };
            _db.SM_FQCBP_H.Add(history);

            // 2. Upsert SM_FQCBP — tăng Qty cho cả PASS và NG
            var summary = await _db.SM_FQCBP
                .FirstOrDefaultAsync(x => x.WorkOrder == workOrder);

            if (summary == null)
            {
                summary = new SM_FQCBP
                {
                    WorkOrder = workOrder,
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
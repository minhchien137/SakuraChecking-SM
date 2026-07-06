
using Microsoft.EntityFrameworkCore;
using ScanCheckSakura.Data;
using ScanCheckSakura.Models;

namespace ScanCheckSakura.Services.FGServices
{
    public class CBCPService : ICBCPService
    {

        private readonly ApplicationDbContext _db;

        public CBCPService(ApplicationDbContext db)
        {
            _db = db;
        }
        
        /* 
           EAN-13 của hộp màu → tên màu
       */

        private static readonly Dictionary<string, string> _boxColorMap = new()
        {
            { "7090045253168", "Pink" },
            { "7090045253151", "Blue" },
            { "7090045253175", "Green" },
        };

        /*  

         Variant code (ký tự 6-7 trong ProductSN) → tên màu sản phẩm
         Ví dụ: RM15A[00]50500001 → "00" → Blue
        */
        private static readonly Dictionary<string, string> _variantColorMap = new()
        {
            { "00", "Blue"  },
            { "01", "Pink"  },
            { "02", "Green" },
        };


        public CBCPViewModel.CheckResult Check(string boxSN, string productSN)
        {
            // Bước 1: Làm sạch input 
            boxSN = (boxSN ?? "").Trim();
            productSN = (productSN ?? "").Trim().ToUpperInvariant();

            //  Bước 2: Validate BoxSN 
            if (!IsValidBoxSN(boxSN))
            {
                return Fail(
                    errorMessage: $"色盒序列号无效: [{boxSN}]. 需要13位数字."
                );
            }

            // Bước 3: Validate ProductSN
            if (!IsValidProductSN(productSN))
            {
                return Fail(
                    errorMessage: $"产品序列号无效: [{productSN}]. " +
                                  "需要15个字符，以RM15A开头."
                );
            }

            // Bước 4: Xác định màu hộp
            if (!_boxColorMap.TryGetValue(boxSN, out var boxColor))
            {
                return Fail(
                    errorMessage: $"色盒 [{boxSN}] 不在列表中"
                );
            }

            // Bước 5: Parse Variant → màu sản phẩm 
            // Format: RM15A + Variant(2) + Year(1) + Day(3) + Sequential(4)
            //         01234    56           7          89A      BCDE
            var variantCode = productSN.Substring(5, 2);

            if (!_variantColorMap.TryGetValue(variantCode, out var productColor))
            {
                return new CBCPViewModel.CheckResult
                {
                    IsPass = false,
                    BoxColor = boxColor,
                    ProductColor = null,
                    ErrorMessage = $"变体 [{variantCode}] 未配置."
                };
            }

            // Bước 6: So sánh màu 
            bool colorsMatch = string.Equals(boxColor, productColor,
                                             StringComparison.OrdinalIgnoreCase);

            if (!colorsMatch)
            {
                return new CBCPViewModel.CheckResult
                {
                    IsPass = false,
                    BoxColor = boxColor,
                    ProductColor = productColor,
                    ErrorMessage = $"颜色不匹配 — 色盒: {boxColor} | 产品: {productColor}"
                };
            }

            // ── PASS ──────────────────────────────────────────
            return new CBCPViewModel.CheckResult
            {
                IsPass = true,
                BoxColor = boxColor,
                ProductColor = productColor,
                WorkOrderInfo = $"Variant {variantCode} · {productColor}"
            };
        }



        // ── Lưu log vào DB ────────────────────────────────────
 
        public async Task SaveLogAsync(
            string boxSN,
            string productSN,
            CBCPViewModel.CheckResult result)
        {
            _db.CBCPLogs.Add(new SM_CBCPLog
            {
                BoxSN        = boxSN,
                ProductSN    = productSN,
                BoxColor     = result.BoxColor,
                ProductColor = result.ProductColor,
                Result       = result.IsPass ? "PASS" : "FAIL",
                ErrorMessage = result.IsPass ? null : result.ErrorMessage,
                CheckedAt    = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        // ── Lấy lịch sử gần nhất ─────────────────────────────

        public async Task<List<SM_CBCPLog>> GetRecentLogsAsync(int count = 20)
        {
            return await _db.CBCPLogs
                .OrderByDescending(x => x.CheckedAt)
                .Take(count)
                .ToListAsync();
        }
        


        //  Private helpers

        // Color Box SN hợp lệ: đúng 13 chữ số (EAN-13)

        private static bool IsValidBoxSN(string value)
            => value.Length == 13 && value.All(char.IsDigit);

            /*
         Quy tắc Product SN hợp lệ:
          - Đúng 15 ký tự
          - Bắt đầu bằng "RM15A"
          - Ký tự 6-7 là chữ số (Variant code)
          - Chỉ chứa chữ cái hoa và số (Base34 compatible)
            */
        private static bool IsValidProductSN(string value)
        {
            if (value.Length != 15) return false;
            if (!value.StartsWith("RM15A", StringComparison.OrdinalIgnoreCase)) return false;
            if (!char.IsDigit(value[5]) || !char.IsDigit(value[6])) return false;
            return value.All(c => char.IsLetterOrDigit(c));
        }
        // Tạo nhanh một CheckResult thất bại chỉ với message lỗi
        private static CBCPViewModel.CheckResult Fail(string errorMessage)
            => new()
            {
                IsPass       = false,
                BoxColor     = null,
                ProductColor = null,
                ErrorMessage = errorMessage
            };
    }
}

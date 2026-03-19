using ScanCheckSakura.Models;

namespace ScanCheckSakura.Services.FGServices
{
    public interface ICBCPService
    {
        /*
          Thực hiện toàn bộ logic kiểm tra:
             1. Validate định dạng BoxSN (EAN-13)
             2. Validate định dạng ProductSN (RM15A format)
             3. Xác định màu hộp từ BoxSN
             4. Parse Variant từ ProductSN → màu sản phẩm
             5. So sánh 2 màu → PASS / FAIL
        */
        CBCPViewModel.CheckResult Check(string boxSN, string productSN);

        // Lưu kết quả vào bảng SM_CBCPLog
        Task SaveLogAsync(string boxSN, string productSN, CBCPViewModel.CheckResult result);

        // Lấy N bản ghi gần nhất từ SM_CBCPLog
        Task<List<SM_CBCPLog>> GetRecentLogsAsync(int count = 20);
    }
    
}



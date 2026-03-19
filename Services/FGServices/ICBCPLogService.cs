using ScanCheckSakura.Models;

namespace ScanCheckSakura.Services.FGServices
{
    public interface ICBCPLogService
    {

        // Lấy toàn bộ lịch sử có lọc + phân trang
 
        Task<CBCPLogViewModel> GetLogsAsync(CBCPLogFilter filter);


        // Xuất dữ liệu đã lọc ra file Excel (.xlsx)

        Task<byte[]> ExportExcelAsync(CBCPLogFilter filter);
    }
}

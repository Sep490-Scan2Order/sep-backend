using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Application.Interfaces
{
    public interface IEmailService
    {
        Task<ApiResponse<bool>> SendEmailAsync(string to, string subject, string htmlContent);
    }
}
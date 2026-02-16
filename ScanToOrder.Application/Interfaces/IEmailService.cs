using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Application.Interfaces
{
    public interface IEmailService
    {
        Task<ApiResponse<bool>> SendEmailAsync(string to, string subject, string htmlContent);
        Task<ApiResponse<bool>> SendEmailWithTemplateAsync(string to, string subject, string templateId, object templateParams);
    }
}
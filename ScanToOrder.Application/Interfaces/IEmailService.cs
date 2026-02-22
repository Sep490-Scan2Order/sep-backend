namespace ScanToOrder.Application.Interfaces
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string to, string subject, string htmlContent);
        Task<bool> SendEmailWithTemplateAsync(string to, string subject, string templateId, object templateParams);
    }
}
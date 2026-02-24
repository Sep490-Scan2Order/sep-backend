namespace ScanToOrder.Application.Interfaces
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string to, string subject, string htmlContent);

        Task<bool> SendEmailViaIdDomainAsync(string to, string subject, string htmlContent);

        Task<bool> SendEmailViaIoDomainAsync(string to, string subject, string htmlContent);

        Task<bool> SendEmailWithTemplateIdDomainAsync(
                string to,
                string subject,
                string templateId,
                object templateParams);

        Task<bool> SendEmailWithTemplateIoDomainAsync(
                string to,
                string subject,
                string templateId,
                object templateParams);

    }
}
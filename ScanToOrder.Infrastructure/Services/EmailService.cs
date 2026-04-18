using Microsoft.Extensions.Logging;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Infrastructure.Configuration;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Exceptions;

namespace ScanToOrder.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private readonly HttpClient _httpClient;
        private readonly IOptionsSnapshot<EmailSettings> _emailOptions;
        private readonly ILogger<EmailService> _logger;
        public EmailService(
             HttpClient httpClient,
             IOptionsSnapshot<EmailSettings> emailOptions,
             ILogger<EmailService> logger)
        {
            _httpClient = httpClient;
            _emailOptions = emailOptions;
            _logger = logger;
        }
        private EmailSettings GetIOSettings(string toEmail)
        {
            return _emailOptions.Get(EmailMessage.EmailDomain.IO_DOMAIN);
        }
        private EmailSettings GetIDSettings(string toEmail)
        {
            return _emailOptions.Get(EmailMessage.EmailDomain.ID_DOMAIN);
        }

        private EmailSettings GetIDsSettings(IEnumerable<string> toEmail)
        {
            return _emailOptions.Get(EmailMessage.EmailDomain.ID_DOMAIN);
        }

        public async Task<bool> SendEmailViaIoDomainAsync(string to, string subject, string htmlContent)
        {
            var settings = _emailOptions.Get(EmailMessage.EmailDomain.IO_DOMAIN);
            return await SendRequestInternalAsync(settings, to, subject, htmlContent);
        }

        public async Task<bool> SendEmailViaIdDomainAsync(string to, string subject, string htmlContent)
        {
            var settings = _emailOptions.Get(EmailMessage.EmailDomain.ID_DOMAIN);
            return await SendRequestInternalAsync(settings, to, subject, htmlContent);
        }

        private async Task<bool> SendRequestInternalAsync(EmailSettings settings, string to, string subject, string htmlContent)
        {
            var requestData = new
            {
                from = settings.FromEmail,
                to = new[] { to },
                subject,
                html = htmlContent
            };

            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {settings.ApiKey}");

            var response = await _httpClient.PostAsync(settings.ApiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"{EmailMessage.EmailSuccess.EMAIL_SENT} tới {to} qua {settings.FromEmail}");
                return true;
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Gửi email thất bại qua {FromEmail}. HTTP {StatusCode}. Chi tiết: {ErrorContent}", settings.FromEmail, response.StatusCode, errorContent);
            throw new DomainException("Hệ thống gửi email đang gặp sự cố. Vui lòng thử lại sau ít phút hoặc liên hệ hỗ trợ.");
        }

        private async Task<bool> GuestRequestInternalAsync(EmailSettings settings, string guestEmail, string subject, string htmlContent)
        {
            var requestData = new
            {
                from = $"{guestEmail} <{settings.FromEmail}>",
                to = settings.ToEmail,
                subject,
                html = htmlContent,
                reply_to = guestEmail
            };

            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {settings.ApiKey}");

            var response = await _httpClient.PostAsync(settings.ApiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"{EmailMessage.EmailSuccess.EMAIL_SENT} từ {guestEmail} qua {settings.ToEmail}");
                return true;
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Nhận email thất bại qua {ToEmail}. HTTP {StatusCode}. Chi tiết: {ErrorContent}", settings.ToEmail, response.StatusCode, errorContent);
            throw new DomainException("Hệ thống gửi email đang gặp sự cố. Vui lòng thử lại sau ít phút hoặc liên hệ hỗ trợ.");
        }

        public async Task<bool> GuestSendEmailAsync(string from, string subject, string htmlContent)
        {
            var settings = _emailOptions.Get(EmailMessage.EmailDomain.ID_DOMAIN);
            return await GuestRequestInternalAsync(settings, from, subject, htmlContent);
        }

        public async Task<bool> SendEmailAsync(string to, string subject, string htmlContent)
        {
            var settings = _emailOptions.Get(EmailMessage.EmailDomain.ID_DOMAIN);

            return await SendRequestInternalAsync(settings, to, subject, htmlContent);
        }

        public async Task<bool> SendEmailWithTemplateIdDomainAsync(
                string to,
                string subject,
                string templateId,
                object templateParams)
        {
            var settings = GetIDSettings(to);
            var requestData = new
            {
                from = settings.FromEmail,
                to = new[] { to },
                subject,    
                template = new
                {
                    id = templateId,
                    variables = templateParams
                }
            };

            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {settings.ApiKey}");

            var response = await _httpClient.PostAsync(settings.ApiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"{EmailMessage.EmailSuccess.EMAIL_SENT_VIA_TEMPLATE} tới {to}");
                return true;
            }

            _logger.LogError("Gửi email bằng template thất bại (tới {To}). Chi tiết: {ResponseBody}", to, responseBody);
            throw new DomainException("Không thể gửi email lúc này. Vui lòng kiểm tra lại địa chỉ email hoặc thử lại sau.");
        }

        public async Task<bool> SendEmailsWithTemplateIdDomainAsync(
                IEnumerable<string> to,
                string subject,
                string templateId,
                object templateParams)
        {
            var settings = GetIDsSettings(to);
            var requestData = new
            {
                from = settings.FromEmail,
                to = to.ToArray(),
                subject,
                template = new
                {
                    id = templateId,
                    variables = templateParams
                }
            };

            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {settings.ApiKey}");

            var response = await _httpClient.PostAsync(settings.ApiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"{EmailMessage.EmailSuccess.EMAIL_SENT_VIA_TEMPLATE} tới {to}");
                return true;
            }

            _logger.LogError("Gửi email hàng loạt bằng template thất bại. Chi tiết: {ResponseBody}", responseBody);
            throw new DomainException("Không thể gửi email lúc này. Vui lòng kiểm tra lại danh sách địa chỉ email hoặc thử lại sau.");
        }

        public async Task<bool> SendEmailWithTemplateIoDomainAsync(
                string to,
                string subject,
                string templateId,
                object templateParams)
        {
            var settings = GetIOSettings(to);
            var requestData = new
            {
                from = settings.FromEmail,
                to = new[] { to },
                subject,
                template = new
                {
                    id = templateId,
                    variables = templateParams
                }
            };

            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {settings.ApiKey}");

            var response = await _httpClient.PostAsync(settings.ApiUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"{EmailMessage.EmailSuccess.EMAIL_SENT_VIA_TEMPLATE} tới {to}");
                return true;
            }

            _logger.LogError("Gửi email bằng template (IO domain) thất bại (tới {To}). Chi tiết: {ResponseBody}", to, responseBody);
            throw new DomainException("Không thể gửi email lúc này. Vui lòng kiểm tra lại địa chỉ email hoặc thử lại sau.");
        }
    }
}

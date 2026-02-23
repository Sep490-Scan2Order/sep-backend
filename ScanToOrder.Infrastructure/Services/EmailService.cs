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
            _logger.LogError($"Failed to send email via {settings.FromEmail}. Error: {errorContent}");
            throw new DomainException($"Email service error: {response.StatusCode}");
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

            _logger.LogError($"Resend Error: {responseBody}");
            throw new DomainException(responseBody);
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

            _logger.LogError($"Resend Error: {responseBody}");
            throw new DomainException(responseBody);
        }
    }
}

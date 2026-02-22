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
        private readonly EmailSettings _emailSettings;
        private readonly ILogger<EmailService> _logger;
        public EmailService(
             HttpClient httpClient,
             IOptions<EmailSettings> emailOptions,
             ILogger<EmailService> logger)
        {
            _httpClient = httpClient;
            _emailSettings = emailOptions.Value;
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(string to, string subject, string htmlContent)
        {
            var requestData = new
            {
                from = _emailSettings.FromEmail,
                to = new[] { to },
                subject,
                html = htmlContent
            };

            var json = JsonSerializer.Serialize(requestData);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_emailSettings.ApiKey}");

            var response = await _httpClient.PostAsync(_emailSettings.ApiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"{EmailMessage.EmailSuccess.EMAIL_SENT} tới {to}");
                return true;
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError($"Failed to send email to {to}. Status: {response.StatusCode}, Error: {errorContent}");
            throw new DomainException($"Failed to send email: {response.StatusCode}");
        }

        public async Task<bool> SendEmailWithTemplateAsync(
                string to,
                string subject,
                string templateId,
                object templateParams)
        {
            var requestData = new
            {
                from = _emailSettings.FromEmail,
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
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_emailSettings.ApiKey}");

            var response = await _httpClient.PostAsync(_emailSettings.ApiUrl, content);
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

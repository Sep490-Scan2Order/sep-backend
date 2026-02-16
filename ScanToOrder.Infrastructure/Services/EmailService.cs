using Microsoft.Extensions.Logging;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Infrastructure.Configuration;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

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

        public async Task<ApiResponse<bool>> SendEmailAsync(string to, string subject, string htmlContent)
        {
            try
            {
                var requestData = new
                {
                    from = _emailSettings.FromEmail,
                    to = new[] { to },
                    subject = subject,
                    html = htmlContent
                };

                var json = JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_emailSettings.ApiKey}");

                var response = await _httpClient.PostAsync("https://api.resend.com/emails", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation($"Email sent successfully to {to}");
                    return new ApiResponse<bool>
                    {
                        IsSuccess = true,
                        Message = "Email sent successfully",
                        Data = true
                    };
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to send email to {to}. Status: {response.StatusCode}, Error: {errorContent}");
                    _logger.LogError($"Resend Error Detail: {errorContent}");
                    return new ApiResponse<bool>
                    {
                        IsSuccess = false,
                        Message = $"Failed to send email: {response.StatusCode}",
                        Data = false
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending email to {to}");
                return new ApiResponse<bool>
                {
                    IsSuccess = false,
                    Message = $"Error sending email: {ex.Message}",
                    Data = false
                };
            }
        }
    }
}

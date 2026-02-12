using Microsoft.Extensions.Options;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ScanToOrder.Infrastructure.Services
{
    public class EsmsSender : ISmsSender
    {
        private readonly HttpClient _httpClient;
        private readonly EsmsSettings _settings;

        public EsmsSender(HttpClient httpClient, IOptions<EsmsSettings> settings)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
        }

        public async Task SendAsync(string phone, string otpCode)
        {
            var message = $"{otpCode} la ma xac minh dang ky Baotrixemay cua ban";

            var requestBody = new
            {
                ApiKey = _settings.ApiKey,
                SecretKey = _settings.SecretKey,
                Brandname = _settings.Brandname,
                SmsType = _settings.SmsType,
                IsUnicode = _settings.IsUnicode,
                campaignid = _settings.CampaignId,
                CallbackUrl = _settings.CallbackUrl,
                Phone = phone,
                Content = message,
                RequestId = Guid.NewGuid().ToString()
            };

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            };

            var bodyJson = JsonSerializer.Serialize(requestBody, jsonOptions);

            try
            {
                var url = "https://rest.esms.vn/MainService.svc/json/SendMultipleMessage_V4_post_json/";

                var content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                var response = await _httpClient.PostAsync(url, content);
                var contentString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"eSMS HTTP {(int)response.StatusCode}: {contentString}");
                }
                try
                {
                    using var doc = JsonDocument.Parse(contentString);
                    if (doc.RootElement.TryGetProperty("CodeResult", out var codeResultElement))
                    {
                        var codeResult = codeResultElement.GetString();
                        if (codeResult != "100")
                        {
                            var errorMessage = doc.RootElement.TryGetProperty("ErrorMessage", out var errorElement)
                                ? errorElement.GetString()
                                : "Unknown error";
                            throw new Exception($"eSMS error CodeResult={codeResult}, Message={errorMessage}");
                        }
                    }
                }
                catch (JsonException)
                {
                    throw new Exception($"Không parse được response eSMS: {contentString}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Lỗi gửi SMS: {ex.Message}");
            }
        }
    }
}

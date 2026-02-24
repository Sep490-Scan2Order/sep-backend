using Microsoft.Extensions.Logging; // Thêm thư viện logging
using Microsoft.Extensions.Options;
using ScanToOrder.Application.DTOs.External;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Infrastructure.Configuration;
using System.Net.Http.Json;

namespace ScanToOrder.Infrastructure.Services
{
    public class TaxService : ITaxService
    {
        private readonly HttpClient _httpClient;
        private readonly N8NSettings _settings;
        private readonly ILogger<TaxService> _logger;

        public TaxService(
            HttpClient httpClient,
            IOptions<N8NSettings> options,
            ILogger<TaxService> logger)
        {
            _httpClient = httpClient;
            _settings = options.Value;
            _logger = logger;
        }

        public async Task<bool> IsTaxCodeValidAsync(string taxCode)
        {
            if (string.IsNullOrWhiteSpace(taxCode)) return false;

            try
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(_settings.TaxValidationUrl),
                    Content = JsonContent.Create(new { taxCode = taxCode })
                };

                _logger.LogInformation("Đang gọi GET (with Body) tới n8n cho MST: {TaxCode}", taxCode);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("n8n báo lỗi HTTP {Status}: {Content}", response.StatusCode, errorBody);
                    throw new Exception("Hệ thống kiểm tra mã số thuế không phản hồi.");
                }

                var result = await response.Content.ReadFromJsonAsync<TaxValidationResponse>();

                _logger.LogInformation("Kết quả tra cứu n8n: {Status}", result?.taxStatus);

                return result != null && string.Equals(result.taxStatus?.Trim(), "Đang hoạt động",
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thực hiện IsTaxCodeValidAsync");
                throw;
            }
        }

        public async Task<TaxLookupResult> GetTaxCodeDetailsAsync(string taxCode)
        {
            if (string.IsNullOrWhiteSpace(taxCode)) return null;

            try
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(_settings.TaxValidationUrl),
                    Content = JsonContent.Create(new { taxCode = taxCode })
                };

                _logger.LogInformation("Đang gọi n8n (GET with Body) cho MST: {TaxCode}", taxCode);

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError("n8n báo lỗi HTTP {Status}: {Content}", response.StatusCode, errorBody);
                    throw new Exception("Hệ thống kiểm tra mã số thuế không phản hồi.");
                }

                var jsonString = await response.Content.ReadAsStringAsync();

                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                TaxValidationResponse item = null;

                if (jsonString.Trim().StartsWith("["))
                {
                    var results =
                        System.Text.Json.JsonSerializer.Deserialize<List<TaxValidationResponse>>(jsonString, options);
                    item = results?.FirstOrDefault();
                }
                else
                {
                    item = System.Text.Json.JsonSerializer.Deserialize<TaxValidationResponse>(jsonString, options);
                }

                if (item == null) return null;

                var result = new TaxLookupResult
                {
                    TaxCode = item.taxCode,
                    FullName = item.fullName,
                    Status = item.taxStatus,
                    IsValid = string.Equals(item.taxStatus?.Trim(), "Đang hoạt động",
                        StringComparison.OrdinalIgnoreCase)
                };

                if (item.raw != null)
                {
                    if (item.raw.TryGetValue("Mã số thuế cá nhân", out var personalCode))
                    {
                        result.IsPersonal = true;
                        result.TaxCode = personalCode?.ToString();
                    }
                    else if (item.raw.TryGetValue("Mã số thuế doanh nghiệp", out var businessCode))
                    {
                        result.IsPersonal = false;
                        result.TaxCode = businessCode?.ToString();
                    }

                    if (item.raw.TryGetValue("Người đại diện", out var rep))
                        result.Representative = rep?.ToString();

                    if (item.raw.TryGetValue("Quản lý bởi", out var manager))
                        result.ManagedBy = manager?.ToString();
                }

                _logger.LogInformation("Đã lấy thông tin MST {TaxCode} thành công", result.TaxCode);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thực hiện GetTaxCodeDetailsAsync cho MST: {TaxCode}", taxCode);
                throw;
            }
        }
    }
}
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using ScanToOrder.Application.DTOs.External;
using ScanToOrder.Application.Interfaces;

namespace ScanToOrder.Infrastructure.Services;

public class BankLookupService : IBankLookupService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BankLookupService> _logger;

    public BankLookupService(HttpClient httpClient, ILogger<BankLookupService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<BankLookResponse> LookupAccountAsync(BankLookRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("", request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("API BankLookup lỗi hệ thống: {StatusCode} - {Content}", response.StatusCode,
                    errorContent);

                return new BankLookResponse
                {
                    Success = false,
                    Msg = $"Lỗi kết nối API: {response.StatusCode}"
                };
            }

            var result = await response.Content.ReadFromJsonAsync<BankLookResponse>();

            if (result != null && result.Success)
            {
                _logger.LogInformation("Truy vấn thành công. Chủ tài khoản: {OwnerName}", result.Data?.OwnerName);
            }
            else
            {
                _logger.LogWarning("API trả về thất bại: {Msg}", result?.Msg);
            }

            return result;
        }
        catch (Exception ex)
        {
            return new BankLookResponse
            {
                Success = false,
                Msg = "Đã xảy ra lỗi trong quá trình xử lý yêu cầu."
            };
        }
    }
}
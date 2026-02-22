namespace ScanToOrder.Infrastructure.Configuration;

public class PayOSSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChecksumKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
}
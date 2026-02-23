namespace ScanToOrder.Infrastructure.Configuration;

public class BankLookupSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
}
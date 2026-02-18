namespace ScanToOrder.Application.DTOs.Configuration;

public class ConfigurationResponse
{
    public int VoucherRate { get; set; }
    public int CommissionRate { get; set; }
    public int ExpiredDuration { get; set; }
    public int RedeemRate { get; set; }
    public DateOnly LastUpdated { get; set; }
}
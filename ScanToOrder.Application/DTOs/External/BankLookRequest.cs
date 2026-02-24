namespace ScanToOrder.Application.DTOs.External;

public class BankLookRequest
{
    public string Bank { get; set; } = string.Empty;    
    public string Account { get; set; } = string.Empty;
}
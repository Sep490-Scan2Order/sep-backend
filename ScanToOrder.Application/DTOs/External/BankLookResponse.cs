namespace ScanToOrder.Application.DTOs.External;

public class BankLookResponse
{
    public int Code { get; set; }
    public bool Success { get; set; }
    public BankLookData Data { get; set; }
    public string Msg { get; set; }
}

public class BankLookData
{
    public string OwnerName { get; set; }
}
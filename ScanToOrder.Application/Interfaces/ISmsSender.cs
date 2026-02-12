namespace ScanToOrder.Application.Interfaces;

public interface ISmsSender
{
    Task SendAsync(string phone, string message);
}


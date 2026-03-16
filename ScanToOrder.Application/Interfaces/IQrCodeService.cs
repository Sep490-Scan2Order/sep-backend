namespace ScanToOrder.Application.Interfaces
{
    public interface IQrCodeService
    {
        byte[] GenerateRestaurantQrCodeBytes(string restaurantId);

        byte[] GenerateQrCodeBytes(string content);
    }
}

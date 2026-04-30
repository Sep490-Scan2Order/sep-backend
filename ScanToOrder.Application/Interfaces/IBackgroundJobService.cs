namespace ScanToOrder.Application.Interfaces;

public interface IBackgroundJobService
{
    void EnqueueSearchIndexDish(int dishId);
    void EnqueueSearchIndexRestaurant(int restaurantId);
    void EnqueueFullReIndex();
    void EnqueueUploadOrderQr(byte[] qrBytes, Guid orderId);
    void EnqueueGeneratePaymentAudio(int orderCode, decimal amount);
}

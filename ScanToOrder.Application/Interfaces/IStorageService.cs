using ScanToOrder.Application.DTOs.Storage;

namespace ScanToOrder.Application.Interfaces
{
    public interface IStorageService
    {
        Task<string> UploadFromBytesAsync(byte[] imageBytes, string fileName, string bucketName = "restaurant_qrCode");
        Task<string> GetOrGenerateOrderAudioAsync(int orderNumber, string textToSpeak);
    }
}

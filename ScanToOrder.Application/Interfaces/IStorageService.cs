using ScanToOrder.Application.DTOs.Storage;

namespace ScanToOrder.Application.Interfaces
{
    public interface IStorageService
    {
        Task<string> UploadQrCodeFromBytesAsync(byte[] imageBytes, string fileName, string bucketName = "restaurant_qrCode");
    }
}

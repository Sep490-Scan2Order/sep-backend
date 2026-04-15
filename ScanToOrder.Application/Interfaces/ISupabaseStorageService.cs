
namespace ScanToOrder.Application.Interfaces
{
    public interface ISupabaseStorageService
    {
        Task UploadAsync(string bucket, byte[] bytes, string fileName, string contentType);
        string GetPublicUrl(string bucket, string fileName);
    }
}

using Microsoft.Extensions.Configuration;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Exceptions;

namespace ScanToOrder.Infrastructure.Services
{
    public class StorageService : IStorageService
    {
        private readonly Supabase.Client _supabase;

        public StorageService(IConfiguration configuration)
        {
            var supabaseUrl = configuration["Supabase:Url"];
            var supabaseKey = configuration["Supabase:Key"];
            _supabase = new Supabase.Client(supabaseUrl!, supabaseKey);
        }

        public async Task<string> UploadQrCodeFromBytesAsync(byte[] imageBytes, string fileName, string bucketName = "restaurant_qrCode")
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                throw new DomainException(StorageMessage.StorageError.FILE_IS_EMPTY);
            }

            try
            {
                await _supabase.Storage
                .From(bucketName)
                .Upload(imageBytes, fileName, new Supabase.Storage.FileOptions { ContentType = "image/png", Upsert = true });

                    return _supabase.Storage.From(bucketName).GetPublicUrl(fileName);
            }
            catch (Exception ex)
            {
                throw new DomainException($"{StorageMessage.StorageError.UPLOAD_FAILED}: {ex.Message}");
            }
        }
    }
}

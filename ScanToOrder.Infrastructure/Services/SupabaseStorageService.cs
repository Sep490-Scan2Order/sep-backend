using ScanToOrder.Application.Interfaces;

namespace ScanToOrder.Infrastructure.Services
{
    public class SupabaseStorageService : ISupabaseStorageService
    {
        private readonly Supabase.Client _client;
        public SupabaseStorageService(Supabase.Client client)
        {
            _client = client;
        }
        public async Task UploadAsync(string bucket, byte[] bytes, string fileName, string contentType)
        {
            await _client.Storage.From(bucket).Upload(bytes, fileName,
                new Supabase.Storage.FileOptions { ContentType = contentType, Upsert = true });
        }

        public string GetPublicUrl(string bucket, string fileName) =>
            _client.Storage.From(bucket).GetPublicUrl(fileName);
    }
}

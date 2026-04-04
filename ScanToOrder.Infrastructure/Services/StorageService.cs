using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Infrastructure.Configuration;

namespace ScanToOrder.Infrastructure.Services
{
    public class StorageService : IStorageService
    {
        private readonly Supabase.Client _supabase;
        private readonly HttpClient _httpClient;

        private readonly string _vpsBaseUrl;
        private readonly string _uploadApiUrl;
        private readonly string _openAiApiKey;
        private readonly string _openAiSpeechUrl;

        public StorageService(
            IOptions<SupabaseSettings> supabaseOptions,
            IOptions<VpsSettings> vpsOptions,
            IOptions<OpenAiSettings> openAiOptions,
            HttpClient httpClient)
        {
            _httpClient = httpClient;
            _supabase = new Supabase.Client(supabaseOptions.Value.Url, supabaseOptions.Value.Key);
            _vpsBaseUrl = vpsOptions.Value.VpsBaseUrl;
            _uploadApiUrl = vpsOptions.Value.UploadApiUrl;
            _openAiApiKey = openAiOptions.Value.ApiKey;
            _openAiSpeechUrl = openAiOptions.Value.SpeechUrl;
        }

        public async Task<string> UploadFromBytesAsync(byte[] imageBytes, string fileName,
            string bucketName = "restaurant_qrCode")
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                throw new DomainException(StorageMessage.StorageError.FILE_IS_EMPTY);
            }

            try
            {
                await _supabase.Storage
                    .From(bucketName)
                    .Upload(imageBytes, fileName,
                        new Supabase.Storage.FileOptions { ContentType = "image/png", Upsert = true });

                return _supabase.Storage.From(bucketName).GetPublicUrl(fileName);
            }
            catch (Exception ex)
            {
                throw new DomainException($"{StorageMessage.StorageError.UPLOAD_FAILED}: {ex.Message}");
            }
        }


        public async Task<string> GetOrGenerateOrderAudioAsync(int orderNumber, string textToSpeak)
        {
            string fileName = $"order_{orderNumber}.mp3";
            string expectedUrl = $"{_vpsBaseUrl}audio/{fileName}";

            if (await CheckFileExistsAsync(expectedUrl))
            {
                return expectedUrl;
            }

            byte[] audioBytes = await GenerateTtsAudioFromOpenAI(textToSpeak);

            await UploadAudioToVpsAsync(audioBytes, fileName);

            return expectedUrl;
        }

        public async Task<string> GetOrGenerateScanAudioAsync(int orderNumber, string textToSpeak)
        {
            string fileName = $"scan_{orderNumber}.mp3";
            string expectedUrl = $"{_vpsBaseUrl}audio/{fileName}";

            if (await CheckFileExistsAsync(expectedUrl))
            {
                return expectedUrl;
            }

            byte[] audioBytes = await GenerateTtsAudioFromOpenAI(textToSpeak);

            await UploadAudioToVpsAsync(audioBytes, fileName);

            return expectedUrl;
        }

        public async Task<string> GetOrGeneratePaymentReceivedAudioAsync(int orderCode, decimal amount)
        {
            string fileName = $"order_{orderCode}_payment.mp3";
            string expectedUrl = $"{_vpsBaseUrl}audio/{fileName}";

            if (await CheckFileExistsAsync(expectedUrl))
                return expectedUrl;
            string textToSpeak = $"Đã nhận được tiền số tiền mặt cho đơn hàng {orderCode} ";
            byte[] audioBytes = await GenerateTtsAudioFromOpenAI(textToSpeak);
            await UploadAudioToVpsAsync(audioBytes, fileName);
            return expectedUrl;
        }

        private async Task<bool> CheckFileExistsAsync(string url)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Head, url);
                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task<byte[]> GenerateTtsAudioFromOpenAI(string text)
        {
            string apiUrl = _openAiSpeechUrl;

            var payload = new
            {
                model = "gpt-4o-mini-tts",
                voice = "cedar",
                input = text,
                instructions =
                    "Generate a clear and natural-sounding audio announcement for the given text, suitable for a restaurant environment. The audio should be concise and easily understandable, with a friendly and inviting tone."
            };

            string jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Headers.Add("Authorization", $"Bearer {_openAiApiKey}");
            request.Content = content;

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsByteArrayAsync(); 
            }

            string errorDetails = await response.Content.ReadAsStringAsync();
            throw new Exception($"Lỗi gọi API OpenAI: {errorDetails}");
        }
        
        private async Task UploadAudioToVpsAsync(byte[] audioBytes, string fileName)
        {
            using var content = new MultipartFormDataContent();

            content.Add(new StringContent(fileName), "filename");

            var audioContent = new ByteArrayContent(audioBytes);
            audioContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/mpeg");
            content.Add(audioContent, "file", fileName);

            var response = await _httpClient.PostAsync(_uploadApiUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Lỗi upload lên VPS: Mã lỗi {response.StatusCode}");
            }
        }

        public async Task<string> UploadOrderQrAsync(byte[] qrBytes, Guid orderId)
        {
            if (qrBytes == null || qrBytes.Length == 0)
                throw new DomainException("QR code rỗng.");

            const string bucketName = "order_qr_codes";
            string fileName = $"orders/{orderId}.png";

            try
            {
                await _supabase.Storage
                    .From(bucketName)
                    .Upload(qrBytes, fileName, new Supabase.Storage.FileOptions
                    {
                        ContentType = "image/png",
                        Upsert = true
                    });

                return _supabase.Storage
                    .From(bucketName)
                    .GetPublicUrl(fileName);
            }
            catch (Exception ex)
            {
                throw new DomainException($"Upload QR thất bại: {ex.Message}");
            }
        }

        public async Task<string> UploadPaymentProofAsync(byte[] imageBytes, string fileName)
        {
            return await UploadFromBytesAsync(imageBytes, fileName, "payment_proofs");
        }

        public string GetOrderQrUrl(Guid orderId)
        {
            const string bucketName = "order_qr_codes";

            string fileName = $"orders/{orderId}.png";

            return _supabase.Storage
                .From(bucketName)
                .GetPublicUrl(fileName);
        }
    }
}
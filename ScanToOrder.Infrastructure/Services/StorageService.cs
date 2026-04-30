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
        private readonly HttpClient _httpClient;
        private readonly ISupabaseStorageService _supabaseStorageService;

        private readonly string _vpsBaseUrl;
        private readonly string _uploadApiUrl;
        private readonly string _openAiApiKey;
        private readonly string _openAiSpeechUrl;

        public StorageService(
            IOptions<VpsSettings> vpsOptions,
            IOptions<OpenAiSettings> openAiOptions,
            HttpClient httpClient,
            ISupabaseStorageService supabaseStorageService)
        {
            _httpClient = httpClient;
            _vpsBaseUrl = vpsOptions.Value.VpsBaseUrl;
            _uploadApiUrl = vpsOptions.Value.UploadApiUrl;
            _openAiApiKey = openAiOptions.Value.ApiKey;
            _openAiSpeechUrl = openAiOptions.Value.SpeechUrl;
            _supabaseStorageService = supabaseStorageService;
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
                await _supabaseStorageService.UploadAsync(bucketName, imageBytes, fileName, "image/png");
                return _supabaseStorageService.GetPublicUrl(bucketName, fileName);
            }
            catch (Exception ex)
            {
                _ = ex;
                throw new DomainException($"{StorageMessage.StorageError.UPLOAD_FAILED}. Vui lòng thử lại sau hoặc liên hệ hỗ trợ nếu vấn đề tiếp tục.");
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
            string fileName = $"order_{orderCode}_{amount}_payment.mp3";
            string expectedUrl = $"{_vpsBaseUrl}audio/{fileName}";

            if (await CheckFileExistsAsync(expectedUrl))
                return expectedUrl;

            string formattedAmount = amount.ToString("N0", new System.Globalization.CultureInfo("vi-VN"));
            string textToSpeak = $"Đã nhận chuyển khoản số tiền {formattedAmount} đồng cho đơn số {orderCode} ";
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
                instructions = "Generate a clear and natural-sounding audio announcement for the given text, suitable for a restaurant environment. The audio should be concise and easily understandable, with a friendly and inviting tone."
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
                await _supabaseStorageService.UploadAsync(bucketName, qrBytes, fileName, "image/png");
                return _supabaseStorageService.GetPublicUrl(bucketName, fileName);
            }
            catch (Exception ex)
            {           
                throw new DomainException($"Tải mã QR lên Supabase thất bại. Lỗi chi tiết: {ex.Message}");
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

            return _supabaseStorageService.GetPublicUrl(bucketName, fileName);
        }
    }
}
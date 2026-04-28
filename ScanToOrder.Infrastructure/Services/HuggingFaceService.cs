using Microsoft.Extensions.Options;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Infrastructure.Configuration;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ScanToOrder.Infrastructure.Services
{
    public class HuggingFaceService : IHuggingFaceService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public HuggingFaceService(HttpClient httpClient, IOptions<AiSettings> aiSettings)
        {
            _httpClient = httpClient;
            var settings = aiSettings.Value;
            _apiKey = settings.HuggingFaceApiKey
                ?? throw new ArgumentNullException(nameof(settings.HuggingFaceApiKey), "HuggingFaceApiKey is missing in configuration.");
        }

        public async Task<byte[]> GenerateImageBytesAsync(string prompt, int width = 512, int height = 1024)
        {
            var url = "https://router.huggingface.co/fal-ai/fal-ai/flux/schnell";
            var request = new HttpRequestMessage(HttpMethod.Post, url);

            if (!string.IsNullOrEmpty(_apiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            }
           
            var payload = new
            {
                prompt = prompt + ", photorealistic, cinematic lighting, shallow depth of field, soft blurry background for text overlay, bokeh, ultra detailed, 8K, masterpiece, professional app wallpaper",
                image_size = new
                {
                    width = width,
                    height = height
                },
                num_inference_steps = 4,
                guidance_scale = 3.5,
                num_images = 1,
                output_format = "png"
            };

            var jsonContent = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new Exception($"Hugging Face API Error ({response.StatusCode}): {errorBody}");
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseBody);
            var imageUrl = doc.RootElement
                .GetProperty("images")[0]
                .GetProperty("url")
                .GetString();

            if (string.IsNullOrEmpty(imageUrl))
            {
                throw new Exception("Fal AI returned empty image URL");
            }

            var imageResponse = await _httpClient.GetAsync(imageUrl);
            if (!imageResponse.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to download generated image from {imageUrl}");
            }

            return await imageResponse.Content.ReadAsByteArrayAsync();
        }
    }
}

using Microsoft.Extensions.Options;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Infrastructure.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
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
            var url = "https://router.huggingface.co/hf-inference/models/stabilityai/stable-diffusion-xl-base-1.0";
            var request = new HttpRequestMessage(HttpMethod.Post, url);

            if (!string.IsNullOrEmpty(_apiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            }

            var payload = new
            {
                inputs = prompt + ", simple flat design, vector style, very blurry background, out of focus, minimalist, high quality, soft colors, low contrast, professional wallpaper",
                parameters = new
                {
                    width = width,
                    height = height,
                    guidance_scale = 8.5
                }
            };

            var jsonContent = JsonSerializer.Serialize(payload);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                throw new Exception($"Hugging Face API Error ({response.StatusCode}): {errorBody}");
            }

            return await response.Content.ReadAsByteArrayAsync();
        }
    }
}

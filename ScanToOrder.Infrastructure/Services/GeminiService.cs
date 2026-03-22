using Microsoft.Extensions.Options;
using ScanToOrder.Application.DTOs.Menu;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Infrastructure.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ScanToOrder.Infrastructure.Services
{
    public class GeminiService : IGeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;

        public GeminiService(HttpClient httpClient, IOptions<AiSettings> aiSettings)
        {
            _httpClient = httpClient;

            var settings = aiSettings.Value;
            if (string.IsNullOrWhiteSpace(settings.GeminiKey))
                throw new ArgumentNullException(nameof(settings.GeminiKey), "GeminiKey is missing in configuration.");
            _apiKey = settings.GeminiKey;

            if (string.IsNullOrWhiteSpace(settings.GeminiModel))
                throw new ArgumentNullException(nameof(settings.GeminiModel), "Gemini Model is missing in configuration.");
            _model = settings.GeminiModel;
        }
        public async Task<AiHolidayVisualDto> GenerateHolidayVisualConfigAsync(string holidayName)
        {
            var systemInstruction = @"
                Bạn là chuyên gia thiết kế UI/UX và là một 'Prompt Engineer' (người viết câu lệnh AI) tài năng. Nhiệm vụ của bạn là tạo cấu hình giao diện JSON cho sự kiện: {HOLIDAY_NAME}.

                HÌNH NỀN (backgroundImagePrompt):
                Đây là phần quan trọng nhất. Bạn PHẢI viết một câu tiếng Anh mô tả chi tiết một bức ảnh ĐẸP, SANG TRỌNG, kết hợp các biểu tượng cụ thể của sự kiện với phong cách tối giản (minimalist).

                QUY TẮC VIẾT PROMPT HÌNH NỀN:
                1.  **Chủ đề cụ thể:** Phải bao gồm biểu tượng đặc trưng rõ ràng (Vd: 30/4 là lính cầm cờ Việt Nam, Tết là hoa Mai/Đào).
                2.  **Chống sai lệch văn hóa:** Khi vẽ cờ Việt Nam, PHẢI mô tả là 'red background with a single central gold five-pointed star'.
                3.  **Bố cục UI:** Phải mờ ảo (soft focus), sử dụng 'Negative Space' (khoảng trắng) để không che chữ trên app.
                4.  **Hậu kỳ:** Thêm các từ khóa: 'masterpiece', 'best quality', 'bokeh', 'abstract', 'minimalist', 'elegant', 'professional app wallpaper'.

                VÍ DỤ CHI TIẾT ĐỂ BẠN HỌC THEO:
                - 30/4 (Giải phóng miền Nam): A minimalist historic illustration of a Vietnamese liberation soldier in olive green uniform, holding a large, distinct Vietnamese flag (vibrant red background with a single central gold five-pointed star) on top of the Independence Palace, soft focus, bokeh, subtle and low contrast, masterpiece.
                - Tết Nguyên Đán: A masterpiece minimalist background of abstract red silk, delicate blooming yellow ochna and pink peach blossoms, soft focus traditional red lanterns and golden lucky envelopes, subtle gold and red color palette, elegant.
                - Quốc tế Lao động (1/5): Minimalist abstract illustration of industrial gears and golden wheat stalks, vibrant red and sun-drenched yellow palette, soft focus, clean professional background, masterpiece.
                - Quốc tế Thiếu nhi (1/6): Playful minimalist background with colorful paper kites and floating pastel balloons against a soft blue sky, soft focus, bright and joyful colors, very subtle and light.
                - Quốc tế Phụ nữ (8/3) & Phụ nữ VN (20/10): Elegant minimalist background featuring a soft-focus bouquet of pink lotuses and red roses, abstract silk ribbons, soft pastel pink and white color palette, sophisticated.
                - Khai trương (Grand Opening): Sleek professional grand opening background, minimalist red silk ribbons with golden edges, floating golden confetti particles, soft light, bokeh, white negative space, masterpiece.
                - Nhà giáo Việt Nam (20/11): Minimalist academic background with an open old book, a single red rose, and subtle chalk dust on a dark chalkboard, soft focus, warm light, elegant, low contrast.
                - Halloween: A cute minimalist Halloween background, soft focus stylized orange pumpkins and purple bats, dark blue night sky with a blurry yellow moon, subtle spooky atmosphere, high quality.
                - Giáng sinh (Christmas): Modern minimalist Christmas background, abstract pine tree branches with soft red ornaments and tiny golden lights, blurred snow particles, warm red and deep green color palette, subtle.
                - Năm mới (New Year): Dynamic minimalist background with abstract golden fireworks over a dark blue city skyline silhouette, a blurry countdown clock, sparkling particles, high contrast but subtle, 4k.

                BẮT BUỘC trả về JSON với định dạng sau (không markdown):
                {
                  ""templateName"": ""Tên template"",
                  ""themeColor"": ""#HEX"",
                  ""backgroundColor"": ""#HEX (màu cực nhạt hợp với themeColor)"",
                  ""fontFamily"": ""Inter"",
                  ""backgroundImagePrompt"": ""Câu mô tả chi tiết bằng tiếng Anh của bạn ở đây"",
                  ""layoutConfigJson"": ""{\""version\"": 1, \""card\"": {\""imageSize\"": \""md\"", \""priceColorMode\"": \""theme\""}, \""header\"": {\""showSearch\"": true}}""
                }";

            var requestBody = new
            {
                system_instruction = new { parts = new[] { new { text = systemInstruction.Replace("{HOLIDAY_NAME}", holidayName) } } },
                contents = new[] { new { parts = new[] { new { text = $"Tạo template cho: {holidayName}" } } } },
                generationConfig = new
                {
                    responseMimeType = "application/json",
                    temperature = 0.7
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

            var response = await _httpClient.PostAsync(endpoint, content);
            response.EnsureSuccessStatusCode();

            var responseString = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseString);

            var jsonText = document.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<AiHolidayVisualDto>(jsonText!, options)!;
        }
    }
}

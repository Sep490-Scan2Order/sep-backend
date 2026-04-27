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
                Đây là phần quan trọng nhất. Bạn PHẢI viết một câu tiếng Anh mô tả chi tiết một bức ảnh CHÂN THẬT (photorealistic), SANG TRỌNG, SỐNG ĐỘNG, nhìn vào là nhận ra ngay ngày lễ gì.

                QUY TẮC VIẾT PROMPT HÌNH NỀN:
                1.  **Phong cách bắt buộc:** Photorealistic, cinematic lighting, ultra detailed, 8K quality. KHÔNG dùng flat design hay vector style.
                2.  **Biểu tượng đặc trưng:** PHẢI có ít nhất 2-3 biểu tượng mang tính nhận diện cao của ngày lễ, được mô tả chi tiết về màu sắc, chất liệu, kích thước.
                3.  **Chống sai lệch văn hóa:** Khi vẽ cờ Việt Nam, PHẢI mô tả là 'red flag with a single central gold five-pointed star'. Khi vẽ ngày lễ Việt Nam, phải mô tả đúng văn hóa Việt Nam.
                4.  **Bố cục UI:** Các vật thể chính đặt ở phần dưới hoặc 2 bên, để trống phần trên và giữa (negative space) cho text overlay. Dùng shallow depth of field (f/1.4) để nền mờ tự nhiên.
                5.  **Ánh sáng & Không khí:** Mô tả cụ thể nguồn sáng (golden hour, warm candlelight, moonlight, neon glow...), và cảm xúc mong muốn (festive, solemn, joyful, romantic...).
                6.  **Chất lượng:** Kết thúc prompt bằng: 'cinematic composition, shallow depth of field, bokeh, photorealistic, ultra detailed, 8K, masterpiece'.

                VÍ DỤ CHI TIẾT ĐỂ BẠN HỌC THEO:
                - 30/4 (Giải phóng miền Nam): A photorealistic scene of a Vietnamese soldier in olive green uniform proudly raising the Vietnamese flag (bright red flag with a single central gold five-pointed star) on top of the historic Independence Palace in Saigon, golden sunset light casting long dramatic shadows, red and gold bunting flags fluttering in the wind, crowds celebrating below, cinematic composition, shallow depth of field, warm golden hour lighting, ultra detailed, 8K, masterpiece.
                - Tết Nguyên Đán: A stunning photorealistic still life of Vietnamese Lunar New Year decorations, vibrant yellow ochna blossoms (mai vàng) and soft pink peach blossoms in a traditional red ceramic vase, golden lucky envelopes (lì xì) scattered on a dark wooden table, red silk lanterns glowing warmly in the background with beautiful bokeh, mandarin oranges, warm candlelight atmosphere, cinematic composition, shallow depth of field, ultra detailed, 8K, masterpiece.
                - Quốc tế Lao động (1/5): Photorealistic close-up of strong hands holding golden wheat stalks and industrial tools, dramatic sunrise light breaking through clouds behind a city skyline, red banners with gold lettering softly blurred in the background, warm orange and deep red color palette, cinematic lighting, shallow depth of field, bokeh, ultra detailed, 8K, masterpiece.
                - Quốc tế Thiếu nhi (1/6): Photorealistic colorful scene of helium balloons in pastel rainbow colors floating against a bright blue sky with fluffy white clouds, paper pinwheels spinning in gentle breeze, wrapped gift boxes with colorful ribbons on green grass, joyful and dreamy atmosphere, natural sunlight, cinematic composition, shallow depth of field, bokeh, ultra detailed, 8K, masterpiece.
                - Quốc tế Phụ nữ (8/3) & Phụ nữ VN (20/10): Photorealistic elegant close-up of a luxurious bouquet of fresh pink roses, white lilies and soft purple lavender wrapped in craft paper with satin ribbon, morning dew drops on petals, soft natural window light, romantic and sophisticated atmosphere, cinematic composition, shallow depth of field, beautiful bokeh, ultra detailed, 8K, masterpiece.
                - Khai trương (Grand Opening): Photorealistic scene of a glamorous grand opening ceremony, shiny red silk ribbon being cut with golden scissors, golden confetti and sparkles exploding in the air, champagne glasses with bubbles, warm spotlight illumination, luxurious and celebratory atmosphere, cinematic composition, shallow depth of field, bokeh, ultra detailed, 8K, masterpiece.
                - Nhà giáo Việt Nam (20/11): Photorealistic warm scene of a teacher's desk with an open vintage leather-bound book, a single fresh red rose in a glass vase, a pair of classic reading glasses, soft chalk dust particles floating in warm golden afternoon sunlight streaming through a window, nostalgic and respectful atmosphere, cinematic composition, shallow depth of field, bokeh, ultra detailed, 8K, masterpiece.
                - Halloween: Photorealistic atmospheric Halloween scene with carved jack-o-lanterns glowing with warm orange candlelight on old wooden porch steps, purple and blue fog rolling across the ground, a full moon behind bare tree branches, black cat silhouette, mysterious and enchanting atmosphere, cinematic night lighting, shallow depth of field, beautiful bokeh, ultra detailed, 8K, masterpiece.
                - Giáng sinh (Christmas): Photorealistic cozy Christmas scene with a beautifully decorated pine tree with red and gold ornaments, warm string lights creating golden bokeh, wrapped presents with velvet ribbons, snow falling gently outside a frost-covered window, hot cocoa with marshmallows on a wooden table, warm fireplace glow, cinematic composition, shallow depth of field, ultra detailed, 8K, masterpiece.
                - Năm mới (New Year): Photorealistic spectacular New Year countdown scene with brilliant golden fireworks exploding over a city skyline reflected in water, champagne glasses clinking with golden bubbles, a vintage clock showing midnight, sparkling confetti and streamers, dramatic night sky with vibrant colors, cinematic composition, shallow depth of field, bokeh, ultra detailed, 8K, masterpiece.

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

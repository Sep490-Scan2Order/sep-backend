using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;

namespace ScanToOrder.Api.Controllers
{
    [ApiController]
    [Route("api/ai-upsell")]
    public class AIUpsellController : BaseController
    {
        private readonly IAIUpsellService _aiUpsellService;

        public AIUpsellController(IAIUpsellService aiUpsellService)
        {
            _aiUpsellService = aiUpsellService;
        }

        /// <summary>
        /// Returns the current status of the AI model file.
        /// </summary>
        [HttpGet("status")]
        [AllowAnonymous]
        public IActionResult GetModelStatus()
        {
            var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SmartUpsellModel.zip");
            bool modelFileExists = System.IO.File.Exists(modelPath);

            return Ok(new
            {
                ModelFileExists = modelFileExists,
                ModelFilePath = modelPath,
                Status = modelFileExists
                    ? "✅ AI Model file tồn tại"
                    : "❌ Chưa có model. Hãy gọi POST /api/dev/train-ai"
            });
        }

        /// <summary>
        /// Returns upsell dish recommendations. Automatically falls back across 3 tiers:
        /// Tier 1: AI Matrix Factorization (if model is ready and restaurant has >= 50 orders)
        /// Tier 2: Best-Sellers (most sold dishes in the restaurant)
        /// Tier 3: Random (cold start — no order history yet)
        /// </summary>
        [HttpGet("recommend")]
        [AllowAnonymous]
        public async Task<IActionResult> GetRecommendations(
            [FromQuery] int restaurantId,
            [FromQuery] List<int> dishIds,
            [FromQuery] int top = 3)
        {
            if (!dishIds.Any())
                return BadRequest(ApiResponse<object>.Failure("Vui lòng cung cấp ít nhất 1 dishId."));

            var (resultIds, source) = await _aiUpsellService.GetRecommendationsAsync(restaurantId, dishIds, top);

            if (source == "empty")
                return Ok(ApiResponse<object>.Success(
                    new { DishIds = new List<int>(), Source = "empty" },
                    "Không còn món nào phù hợp để gợi ý."));

            var message = source switch
            {
                "AI_MatrixFactorization" => $"[AI] Gợi ý {resultIds.Count} món dựa trên hành vi mua hàng.",
                "BestSellers_Fallback"   => $"[Best-sellers] Gợi ý {resultIds.Count} món bán chạy nhất của quán.",
                _                        => $"[Random] Gợi ý {resultIds.Count} món ngẫu nhiên (quán chưa đủ dữ liệu)."
            };

            return Ok(ApiResponse<object>.Success(new { DishIds = resultIds, Source = source }, message));
        }
    }
}

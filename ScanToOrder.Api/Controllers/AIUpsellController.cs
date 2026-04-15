using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.ML;
using Microsoft.EntityFrameworkCore;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Models.AI;

namespace ScanToOrder.Api.Controllers
{
    [ApiController]
    [Route("api/ai-upsell")]
    public class AIUpsellController : BaseController
    {
        private readonly PredictionEnginePool<DishCoOccurrence, DishPrediction>? _predictionPool;
        private readonly IUnitOfWork _unitOfWork;

        // Minimum orders needed to trust the AI model for a restaurant
        private const int MinOrdersRequiredForAI = 50;

        public AIUpsellController(
            IUnitOfWork unitOfWork,
            PredictionEnginePool<DishCoOccurrence, DishPrediction>? predictionPool = null)
        {
            _unitOfWork = unitOfWork;
            _predictionPool = predictionPool;
        }

        /// <summary>
        /// Kiểm tra trạng thái hoạt động của AI Model.
        /// </summary>
        [HttpGet("status")]
        [AllowAnonymous]
        public IActionResult GetModelStatus()
        {
            var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SmartUpsellModel.zip");
            bool modelFileExists = System.IO.File.Exists(modelPath);
            bool modelLoaded = _predictionPool != null;

            return Ok(new
            {
                ModelFileExists = modelFileExists,
                ModelLoadedInMemory = modelLoaded,
                ModelFilePath = modelPath,
                Status = modelLoaded
                    ? "✅ AI Model đang HOẠT ĐỘNG"
                    : modelFileExists
                        ? "⚠️ File model tồn tại nhưng chưa load vào RAM, hãy restart API"
                        : "❌ Chưa có model. Hãy gọi POST /api/dev/train-ai"
            });
        }

        /// <summary>
        /// Lấy danh sách món gợi ý Upsell. Tự động fallback 3 tầng:
        /// Tầng 1: AI Matrix Factorization (nếu model sẵn sàng + đủ data >= 50 đơn)
        /// Tầng 2: Best-Sellers (món bán chạy nhất của quán)
        /// Tầng 3: Random (cold start - quán mới chưa có bất kỳ data nào)
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

            // Lấy toàn bộ món hợp lệ đang bán của quán (5-layer filter)
            var validDishes = await _unitOfWork.BranchDishConfigs.GetQueryable()
                .Where(c => c.RestaurantId == restaurantId
                         && !c.IsDeleted
                         && c.IsSelling
                         && !c.IsSoldOut
                         && !c.Dish.IsDeleted)
                .Select(c => c.DishId)
                .ToListAsync();

            // Loại trừ món đã có trong giỏ
            var candidates = validDishes.Except(dishIds).ToList();

            if (!candidates.Any())
                return Ok(ApiResponse<object>.Success(
                    new { DishIds = new List<int>(), Source = "empty" },
                    "Không còn món nào phù hợp để gợi ý."));

            // ═══════════════════════════════════════
            // TẦNG 1: AI Matrix Factorization
            // ═══════════════════════════════════════
            if (_predictionPool != null)
            {
                var orderCount = await _unitOfWork.Orders.GetQueryable()
                    .CountAsync(o => o.RestaurantId == restaurantId && !o.IsDeleted);

                if (orderCount >= MinOrdersRequiredForAI)
                {
                    var scores = new Dictionary<int, float>();

                    foreach (var cartDishId in dishIds)
                    {
                        foreach (var candidateId in candidates)
                        {
                            var prediction = _predictionPool.Predict("UpsellModel", new DishCoOccurrence
                            {
                                TargetDishId = (uint)cartDishId,
                                RecommendedDishId = (uint)candidateId
                            });

                            if (!scores.ContainsKey(candidateId)) scores[candidateId] = 0;
                            scores[candidateId] += prediction.Score;
                        }
                    }

                    var aiResult = scores
                        .OrderByDescending(x => x.Value)
                        .Take(top)
                        .Select(x => x.Key)
                        .ToList();

                    return Ok(ApiResponse<object>.Success(
                        new { DishIds = aiResult, Source = "AI_MatrixFactorization" },
                        $"[AI] Gợi ý {aiResult.Count} món dựa trên hành vi mua hàng."));
                }
            }

            // ═══════════════════════════════════════
            // TẦNG 2: Best-Sellers Fallback
            // ═══════════════════════════════════════
            var bestSellers = await _unitOfWork.OrderDetails.GetQueryable()
                .Where(od => candidates.Contains(od.DishId) && !od.Order.IsDeleted)
                .GroupBy(od => od.DishId)
                .Select(g => new { DishId = g.Key, TotalSold = g.Sum(x => x.Quantity) })
                .OrderByDescending(x => x.TotalSold)
                .Take(top)
                .Select(x => x.DishId)
                .ToListAsync();

            if (bestSellers.Any())
            {
                return Ok(ApiResponse<object>.Success(
                    new { DishIds = bestSellers, Source = "BestSellers_Fallback" },
                    $"[Best-sellers] Gợi ý {bestSellers.Count} món bán chạy nhất của quán."));
            }

            // ═══════════════════════════════════════
            // TẦNG 3: Random - Cold Start
            // ═══════════════════════════════════════
            var random = new Random();
            var randomPicks = candidates
                .OrderBy(_ => random.Next())
                .Take(top)
                .ToList();

            return Ok(ApiResponse<object>.Success(
                new { DishIds = randomPicks, Source = "Random_ColdStart" },
                $"[Random] Gợi ý {randomPicks.Count} món ngẫu nhiên (quán chưa đủ dữ liệu)."));
        }
    }
}

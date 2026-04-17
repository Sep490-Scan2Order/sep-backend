using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.ML;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Models.AI;

namespace ScanToOrder.Infrastructure.Services
{
    public class AIUpsellService : IAIUpsellService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly PredictionEnginePool<DishCoOccurrence, DishPrediction>? _predictionPool;
        private readonly IAIUpsellPredictor? _predictor;
        private readonly Func<int, int, float>? _poolScoreProvider;

        // Minimum number of orders required to trust AI predictions
        private const int MinOrdersRequiredForAI = 50;

        private readonly IPlanLimitationService _planLimitationService;

        public AIUpsellService(
            IUnitOfWork unitOfWork,
            IPlanLimitationService planLimitationService,
            PredictionEnginePool<DishCoOccurrence, DishPrediction>? predictionPool = null,
            IAIUpsellPredictor? predictor = null,
            Func<int, int, float>? poolScoreProvider = null)
        {
            _unitOfWork = unitOfWork;
            _planLimitationService = planLimitationService;
            _predictionPool = predictionPool;
            _predictor = predictor;
            _poolScoreProvider = poolScoreProvider;
        }

        public async Task<(List<int> DishIds, string Source)> GetRecommendationsAsync(
            int restaurantId,
            List<int> cartDishIds,
            int top = 3)
        {
            var features = await _planLimitationService.GetRestaurantFeaturesAsync(restaurantId);
            if (!features.CanUseAIUpsell)
            {
                return (new List<int>(), "Plan-Limited");
            }
            // ── Load all valid selling dishes (5-layer filter) ───────────────
            var validDishes = await _unitOfWork.BranchDishConfigs.GetQueryable()
                .Where(c => c.RestaurantId == restaurantId
                         && !c.IsDeleted
                         && c.IsSelling
                         && !c.IsSoldOut
                         && !c.Dish.IsDeleted)
                .Select(c => new { c.DishId, c.Dish.Type })
                .ToListAsync();

            // ── Combo-Awareness: Exclude child dishes if their parent combo is already in the cart ─
            // If the cart contains ComboId X → do not suggest the individual component dishes already "included" in that combo
            var cartComboIds = cartDishIds
                .Where(id => validDishes.Any(d => d.DishId == id && d.Type == DishType.Combo))
                .ToHashSet();

            var excludedChildDishIds = new HashSet<int>();
            if (cartComboIds.Any())
            {
                var childIds = await _unitOfWork.ComboDetails.GetQueryable()
                    .Where(cd => cartComboIds.Contains(cd.DishId))
                    .Select(cd => cd.ItemDishId)
                    .ToListAsync();

                excludedChildDishIds = childIds.ToHashSet();
            }

            // candidates = all valid dishes excluding: (1) already in cart, (2) child dishes of combos already in cart
            var candidates = validDishes
                .Select(d => d.DishId)
                .Except(cartDishIds)
                .Except(excludedChildDishIds)
                .ToList();

            if (!candidates.Any())
                return (new List<int>(), "empty");

            // TIER 1: AI Matrix Factorization
            if (_predictionPool != null || _predictor != null)
            {
                var orderCount = await _unitOfWork.Orders.GetQueryable()
                    .CountAsync(o => o.RestaurantId == restaurantId && !o.IsDeleted);

                if (orderCount >= MinOrdersRequiredForAI)
                {
                    var scores = new Dictionary<int, float>();

                    foreach (var cartDishId in cartDishIds)
                    {
                        foreach (var candidateId in candidates)
                        {
                            var score = _predictor != null
                                ? _predictor.PredictScore(cartDishId, candidateId)
                                : _poolScoreProvider != null
                                    ? _poolScoreProvider(cartDishId, candidateId)
                                    : _predictionPool!.Predict("UpsellModel", new DishCoOccurrence
                                    {
                                        TargetDishId = (uint)cartDishId,
                                        RecommendedDishId = (uint)candidateId
                                    }).Score;

                            if (!scores.ContainsKey(candidateId)) scores[candidateId] = 0;
                            scores[candidateId] += score;
                        }
                    }

                    var aiResult = scores
                        .OrderByDescending(x => x.Value)
                        .Take(top)
                        .Select(x => x.Key)
                        .ToList();

                    return (aiResult, "AI_MatrixFactorization");
                }
            }

            // TIER 2: Best-Sellers Fallback
            var bestSellers = await _unitOfWork.OrderDetails.GetQueryable()
                .Where(od => candidates.Contains(od.DishId) && !od.Order.IsDeleted)
                .GroupBy(od => od.DishId)
                .Select(g => new { DishId = g.Key, TotalSold = g.Sum(x => x.Quantity) })
                .OrderByDescending(x => x.TotalSold)
                .Take(top)
                .Select(x => x.DishId)
                .ToListAsync();

            if (bestSellers.Any())
                return (bestSellers, "BestSellers_Fallback");

            // TIER 3: Random — Cold Start
            var random = new Random();
            var randomPicks = candidates
                .OrderBy(_ => random.Next())
                .Take(top)
                .ToList();

            return (randomPicks, "Random_ColdStart");
        }
    }

    public interface IAIUpsellPredictor
    {
        float PredictScore(int targetDishId, int recommendedDishId);
    }
}

using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Trainers;
using ScanToOrder.Infrastructure.Models.AI;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Infrastructure.Services
{
    public class AITrainingJob
    {
        private readonly IUnitOfWork _unitOfWork;

        public AITrainingJob(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        [AutomaticRetry(Attempts = 0)]
        public async Task ExecuteAsync()
        {
            // 1. ETL: Fetch successful orders and build dish co-occurrence pairs
            var rawData = await ExtractAndTransformDataAsync();

            if (!rawData.Any())
            {
                return; // Not enough data to train
            }

            // 2. Setup ML.NET Context
            var mlContext = new MLContext();
            
            // 3. Load data into a DataView
            var trainingDataView = mlContext.Data.LoadFromEnumerable(rawData);

            // 4. Map keys (Matrix Factorization requires encoded key columns)
            var dataProcessingPipeline = mlContext.Transforms.Conversion.MapValueToKey(outputColumnName: "TargetDishIdEncoded", inputColumnName: nameof(DishCoOccurrence.TargetDishId))
                .Append(mlContext.Transforms.Conversion.MapValueToKey(outputColumnName: "RecommendedDishIdEncoded", inputColumnName: nameof(DishCoOccurrence.RecommendedDishId)));

            // 5. Configure Matrix Factorization trainer
            MatrixFactorizationTrainer.Options options = new MatrixFactorizationTrainer.Options
            {
                MatrixColumnIndexColumnName = "TargetDishIdEncoded",
                MatrixRowIndexColumnName = "RecommendedDishIdEncoded",
                LabelColumnName = nameof(DishCoOccurrence.CoOccurrence),
                NumberOfIterations = 20,
                ApproximationRank = 100 // Embedding dimension depth
            };

            var trainerEstimator = dataProcessingPipeline.Append(mlContext.Recommendation().Trainers.MatrixFactorization(options));

            // 6. Train the model
            var trainedModel = trainerEstimator.Fit(trainingDataView);

            // 7. Save the model to a zip file in the application base directory
            string modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SmartUpsellModel.zip");
            mlContext.Model.Save(trainedModel, trainingDataView.Schema, modelPath);
        }

        private async Task<List<DishCoOccurrence>> ExtractAndTransformDataAsync()
        {
            // ── STEP 1: Fetch OrderDetails from successfully served orders ──────
            var orderDetails = await _unitOfWork.OrderDetails.GetQueryable()
                .Include(od => od.Order)
                .Where(od => !od.Order.IsDeleted && od.Order.Status == ScanToOrder.Domain.Enums.OrderStatus.Served)
                .Select(od => new { od.OrderId, od.DishId })
                .ToListAsync();

            var groupedByOrder = orderDetails
                .GroupBy(od => od.OrderId)
                .Select(g => g.Select(x => x.DishId).Distinct().ToList())
                .ToList();

            var coOccurrences = new Dictionary<string, DishCoOccurrence>();

            // ── STEP 2: Generate co-occurrence pairs from real order history ────
            foreach (var orderDishes in groupedByOrder)
            {
                for (int i = 0; i < orderDishes.Count; i++)
                {
                    for (int j = 0; j < orderDishes.Count; j++)
                    {
                        if (i == j) continue;

                        AddOrIncrement(coOccurrences, orderDishes[i], orderDishes[j], weight: 1);
                    }
                }
            }

            // ── STEP 3: Inject synthetic pairs from combo structure ──────────────
            // Load all ComboDetails (DishId = parent combo, ItemDishId = component dish)
            var allCombos = await _unitOfWork.ComboDetails.GetQueryable()
                .Include(cd => cd.Dish)           // Dish parent = Combo
                .Where(cd => !cd.Dish.IsDeleted && cd.Dish.Type == ScanToOrder.Domain.Enums.DishType.Combo)
                .Select(cd => new { ComboId = cd.DishId, ItemDishId = cd.ItemDishId })
                .ToListAsync();

            // Group by ComboId → list of component dish IDs
            var comboMap = allCombos
                .GroupBy(cd => cd.ComboId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ItemDishId).ToList());

            // Only process combos that actually appeared in real orders (avoids generating noise pairs)
            var orderedComboIds = orderDetails
                .Select(od => od.DishId)
                .Distinct()
                .Where(id => comboMap.ContainsKey(id))
                .ToHashSet();

            foreach (var comboId in orderedComboIds)
            {
                var items = comboMap[comboId];

                foreach (var itemDishId in items)
                {
                    // Component dish → Parent combo (high boost: selecting one item suggests the full combo)
                    // Weight = 5: Prioritises combo recommendations over random single dishes
                    AddOrIncrement(coOccurrences, targetId: itemDishId, recommendedId: comboId, weight: 5);

                    // Parent combo → Component dish (lower boost: semantic auxiliary signal)
                    AddOrIncrement(coOccurrences, targetId: comboId, recommendedId: itemDishId, weight: 3);

                    // Cross-pair between sibling dishes inside the same combo
                    // (e.g. Fried Chicken & French Fries in the same combo → they are related)
                    foreach (var siblingId in items.Where(x => x != itemDishId))
                    {
                        AddOrIncrement(coOccurrences, targetId: itemDishId, recommendedId: comboId, weight: 2);
                    }
                }
            }

            return coOccurrences.Values.ToList();
        }

        /// <summary>
        /// Adds a new co-occurrence pair or increments the weight of an existing one (targetId → recommendedId).
        /// </summary>
        private static void AddOrIncrement(
            Dictionary<string, DishCoOccurrence> dict,
            int targetId, int recommendedId, float weight)
        {
            string key = $"{targetId}_{recommendedId}";
            if (!dict.ContainsKey(key))
            {
                dict[key] = new DishCoOccurrence
                {
                    TargetDishId = (uint)targetId,
                    RecommendedDishId = (uint)recommendedId,
                    CoOccurrence = 0
                };
            }
            dict[key].CoOccurrence += weight;
        }
    }
}

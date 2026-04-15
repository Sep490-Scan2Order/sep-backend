using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
            // 1. ETL: Lấy dữ liệu đơn hàng thành công và gom nhóm cặp món ăn
            var rawData = await ExtractAndTransformDataAsync();

            if (!rawData.Any())
            {
                return; // Not enough data to train
            }

            // 2. Setup ML.NET Context
            var mlContext = new MLContext();
            
            // 3. Load Data vào DataView
            var trainingDataView = mlContext.Data.LoadFromEnumerable(rawData);

            // 4. Transform Keys (Matrix Factorization yêu cầu mapped keys)
            var dataProcessingPipeline = mlContext.Transforms.Conversion.MapValueToKey(outputColumnName: "TargetDishIdEncoded", inputColumnName: nameof(DishCoOccurrence.TargetDishId))
                .Append(mlContext.Transforms.Conversion.MapValueToKey(outputColumnName: "RecommendedDishIdEncoded", inputColumnName: nameof(DishCoOccurrence.RecommendedDishId)));

            // 5. Cấu hình Model Matrix Factorization
            MatrixFactorizationTrainer.Options options = new MatrixFactorizationTrainer.Options
            {
                MatrixColumnIndexColumnName = "TargetDishIdEncoded",
                MatrixRowIndexColumnName = "RecommendedDishIdEncoded",
                LabelColumnName = nameof(DishCoOccurrence.CoOccurrence),
                NumberOfIterations = 20,
                ApproximationRank = 100 // Độ sâu học thuật
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
            // Fetch relevant order details from successful orders
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

            foreach (var orderDishes in groupedByOrder)
            {
                // Create pairs of dishes within the same order
                for (int i = 0; i < orderDishes.Count; i++)
                {
                    for (int j = 0; j < orderDishes.Count; j++)
                    {
                        if (i == j) continue; // Don't pair with itself

                        int dishA = orderDishes[i];
                        int dishB = orderDishes[j];

                        // TargetDishId = dishA, Recommended = dishB
                        string key = $"{dishA}_{dishB}";

                        if (!coOccurrences.ContainsKey(key))
                        {
                            coOccurrences[key] = new DishCoOccurrence
                            {
                                TargetDishId = (uint)dishA,
                                RecommendedDishId = (uint)dishB,
                                CoOccurrence = 0
                            };
                        }
                        
                        coOccurrences[key].CoOccurrence++;
                    }
                }
            }

            return coOccurrences.Values.ToList();
        }
    }
}

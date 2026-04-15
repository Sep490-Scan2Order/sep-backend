using Microsoft.ML.Data;

namespace ScanToOrder.Infrastructure.Models.AI
{
    public class DishCoOccurrence
    {
        [LoadColumn(0)]
        public uint TargetDishId { get; set; }

        [LoadColumn(1)]
        public uint RecommendedDishId { get; set; }

        [LoadColumn(2)]
        public float CoOccurrence { get; set; }
    }
}

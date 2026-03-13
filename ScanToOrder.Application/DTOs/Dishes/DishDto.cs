using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Application.DTOs.Dishes
{
    public class DishDto
    {   
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = null!;
        public string DishName { get; set; } = null!;
        public decimal Price { get; set; }

        public string Description { get; set; } = null!;
        
        public string ImageUrl { get; set; } = null!;
        public DishType Type { get; set; }
        public bool IsAvailable { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

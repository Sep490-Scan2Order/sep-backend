using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ScanToOrder.Application.DTOs.Restaurant
{
    public class UpdateRestaurantRequest
    {
        public string RestaurantName { get; set; } = null!;
        public string? Address { get; set; }

        [FromForm(Name = "Latitude")]
        [ModelBinder(BinderType = typeof(InvariantNullableDoubleModelBinder))]
        public double? Latitude { get; set; }

        [FromForm(Name = "Longitude")]
        [ModelBinder(BinderType = typeof(InvariantNullableDoubleModelBinder))]
        
        public double? Longitude { get; set; }

        public IFormFile? Image { get; set; }
        public string? Phone { get; set; }
        public string? Description { get; set; }
    }
}


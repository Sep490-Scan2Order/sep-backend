using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.Restaurant
{
    public class RestaurantDto
    {
        public Guid Id { get; set; }

        public Guid TenantId { get; set; }

        public string RestaurantName { get; set; } = string.Empty;

        public string? Address { get; set; }

        public decimal? Longitude { get; set; }

        public decimal? Latitude { get; set; }

        public string? Image { get; set; }

        public string? Phone { get; set; }

        public string? Description { get; set; }

        public string? ProfileUrl { get; set; }

        public string? QrMenu { get; set; }

        public bool? IsActive { get; set; }

        public bool? IsOpened { get; set; }

        public bool? IsReceivingOrders { get; set; }

        public int? TotalOrder { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}

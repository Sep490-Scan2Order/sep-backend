using ScanToOrder.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.User
{
    public class CreateStaffRequest
    {

        public required int RestaurantId { get; set; }
        public required string Email { get; set; }

        public required string Name { get; set; }

        public required string Phone { get; set; }

        public required string Password { get; set; }

    }
}

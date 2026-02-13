using ScanToOrder.Domain.Entities.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Domain.Entities.Dishes
{
    public partial class Dishes : BaseEntity
    {
        public int CategoryId { get; set; }
        public string DishName { get; set; } = null!;
        public int Price { get; set; }

        public string Description { get; set; } = null!;

        public string ImageUrl { get; set; } = null!;

        public bool IsAvailable { get; set; }

        public virtual Category Category { get; set; } = null!;

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.DTOs.Dishes
{
    public class UpdateCategoryRequest
    {
        public required string CategoryName { get; set; }
    }
}

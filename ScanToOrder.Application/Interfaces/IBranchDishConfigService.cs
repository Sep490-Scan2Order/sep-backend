using ScanToOrder.Application.DTOs.Dishes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.Interfaces
{
    public interface IBranchDishConfigService
    {
        Task<BranchDishConfigDto> ConfigDishByRestaurant(CreateBranchDishConfig request);

        Task<List<BranchDishConfigDto>> GetBranchDishByRestaurant(int restaurantId);

        Task<BranchDishConfigDto> ToggleSoldOutAsync(int branchDishConfigId, bool isSoldOut);
    }
}

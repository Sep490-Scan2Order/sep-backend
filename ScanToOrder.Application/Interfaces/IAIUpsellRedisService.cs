using System.Collections.Generic;
using System.Threading.Tasks;

namespace ScanToOrder.Application.Interfaces
{
    public interface IAIUpsellRedisService
    {
        Task SetAIEligibilityAsync(int restaurantId, bool isEligible);
        Task<bool> GetAIEligibilityAsync(int restaurantId);
        Task SetBestSellersAsync(int restaurantId, List<int> dishIds);
        Task<List<int>> GetBestSellersAsync(int restaurantId);
    }
}

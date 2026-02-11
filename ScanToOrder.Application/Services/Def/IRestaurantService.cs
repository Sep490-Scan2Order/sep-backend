using ScanToOrder.Application.DTOs.Restaurant;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.Services.Def
{
    public interface IRestaurantService
    {
        Task<List<RestaurantDto>> GetAllRestaurantsAsync();
    }
}

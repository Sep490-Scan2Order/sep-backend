using ScanToOrder.Application.DTOs.Other;
using ScanToOrder.Application.DTOs.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.Interfaces
{
    public interface IStaffService
    {
        Task<StaffDto> CreateStaff(CreateStaffRequest staffDto);
        Task<PagedResult<StaffDto>> GetAllStaff(int restaurantId, int page, int pageSize);
        Task<List<StaffDto>> GetAvailableCashiers();
        Task<List<StaffDto>> GetStaffByRestaurant(int restaurantId);
    }
}

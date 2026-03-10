using ScanToOrder.Application.DTOs.Shift;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.Interfaces
{
    public  interface IShiftService
    {
        Task<ShiftDto> CheckInShiftAsync(int restaurantId, Guid staffId, decimal openingCashAmount, string? note);
        Task<ShiftDto> CheckOutShiftAsync(int shiftId, decimal closingCashAmount, string? note);
    }
}

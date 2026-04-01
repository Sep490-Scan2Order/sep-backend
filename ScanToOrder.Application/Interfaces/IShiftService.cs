using ScanToOrder.Application.DTOs.Other;
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
        Task<ShiftReportDto> GetShiftReportAsync(int shiftId);
        Task<PagedResult<ShiftReportDto>> GetAllShiftReportsAsync(int restaurantId, int pageIndex, int pageSize, DateTime? from, DateTime? to);
        Task<PagedResult<ShiftReportDto>> GetShiftReportsByStaffAsync(Guid staffId, int pageIndex, int pageSize);
        Task<ShiftDto> GetShiftByIdAsync(Guid staffId);
    }
}

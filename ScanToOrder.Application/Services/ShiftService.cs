using AutoMapper;
using ScanToOrder.Application.DTOs.Shift;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Entities.Shifts;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services
{
    public class ShiftService : IShiftService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public ShiftService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<ShiftDto> CheckInShiftAsync(int restaurantId, Guid staffId, decimal openingCashAmount, string? note)
        {
            var restaurant = await _unitOfWork.Restaurants.GetByIdAsync(restaurantId);

            if (restaurant == null)
            {
                throw new DomainException(Message.RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);
            }

            if (openingCashAmount < restaurant.MinCashAmount)
            {
                throw new DomainException(Message.ShiftMessage.ShiftError.OPENING_CASH_INVALID);
            }

            var activeShift = await _unitOfWork.Shifts
                .FirstOrDefaultAsync(x => x.RestaurantId == restaurantId && x.Status == ShiftStatus.Open);

            if (activeShift != null)
            {
                throw new DomainException(Message.ShiftMessage.ShiftError.SHIFT_ALREADY_OPEN);
            }

            var shift = new Shift
            {
                RestaurantId = restaurantId,
                StaffId = staffId,
                StartDate = DateTime.UtcNow,
                OpeningCashAmount = openingCashAmount,
                Note = note ?? string.Empty,
                Status = ShiftStatus.Open
            };

            await _unitOfWork.Shifts.AddAsync(shift);
            await _unitOfWork.SaveAsync();

            return _mapper.Map<ShiftDto>(shift);
        }

        public async Task<ShiftDto> CheckOutShiftAsync(int shiftId, decimal actualCashAmount, string? note)
        {
            var shift = await _unitOfWork.Shifts.GetByIdAsync(shiftId);

            if (shift == null)
                throw new DomainException(Message.ShiftMessage.ShiftError.SHIFT_NOT_FOUND);

            if (shift.Status != ShiftStatus.Open)
                throw new DomainException(Message.ShiftMessage.ShiftError.SHIFT_ALREADY_CLOSED);

            var transactions = (await _unitOfWork.Transactions
                .FindAsync(t => t.ShiftId == shiftId && t.Status == OrderTransactionStatus.Success))
                .ToList();

            decimal totalCashOrder = transactions
                .Where(t => t.PaymentMethod == PaymentMethod.Cash)
                .Sum(t => t.TotalAmount); 

            decimal totalTransferOrder = transactions
                .Where(t => t.PaymentMethod == PaymentMethod.BankTransfer && t.TotalAmount > 0)
                .Sum(t => t.TotalAmount);

            decimal totalRefundAmount = transactions
                .Where(t => t.TotalAmount < 0)
                .Sum(t => Math.Abs(t.TotalAmount));

            await using var tx = await _unitOfWork.BeginTransactionAsync();
            try
            {
                shift.EndDate = DateTime.UtcNow;
                shift.Status = ShiftStatus.Closed;
                shift.Note = note ?? string.Empty;
                _unitOfWork.Shifts.Update(shift);

                decimal expectedCash = shift.OpeningCashAmount + totalCashOrder;
                decimal difference = actualCashAmount - expectedCash;

                var report = new ShiftReport
                {
                    ShiftId = shiftId,
                    ReportDate = DateTime.UtcNow,
                    TotalCashOrder = totalCashOrder,
                    TotalTransferOrder = totalTransferOrder,
                    TotalRefundAmount = totalRefundAmount,
                    ExpectedCashAmount = expectedCash,
                    ActualCashAmount = actualCashAmount,
                    Difference = difference,
                    Note = note ?? string.Empty
                };

                await _unitOfWork.ShiftReports.AddAsync(report);

                await _unitOfWork.SaveAsync();
                await tx.CommitAsync();

                return _mapper.Map<ShiftDto>(shift);
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }

        public async Task<ShiftReportDto> GetShiftReportAsync(int shiftId)
        {
            var shift = await _unitOfWork.Shifts.GetByIdAsync(shiftId);
            if (shift == null)
                throw new DomainException(Message.ShiftMessage.ShiftError.SHIFT_NOT_FOUND);

            var report = await _unitOfWork.ShiftReports
                .FirstOrDefaultAsync(r => r.ShiftId == shiftId);

            if (report == null)
                throw new DomainException(Message.ShiftMessage.ShiftError.SHIFT_REPORT_NOT_FOUND);

            return new ShiftReportDto
            {
                Id = report.Id,
                ShiftId = report.ShiftId,
                ReportDate = report.ReportDate,
                TotalCashOrder = report.TotalCashOrder,
                TotalTransferOrder = report.TotalTransferOrder,
                TotalRefundAmount = report.TotalRefundAmount,
                ExpectedCashAmount = report.ExpectedCashAmount,
                ActualCashAmount = report.ActualCashAmount,
                Difference = report.Difference,
                ExpectedTotalAmount = shift.OpeningCashAmount + report.TotalCashOrder + report.TotalTransferOrder,
                Note = report.Note
            };
        }

        public async Task<List<ShiftReportDto>> GetAllShiftReportsAsync(int restaurantId, DateTime? from, DateTime? to)
        {
            var rows = await _unitOfWork.ShiftReports
                .GetReportsByRestaurantAsync(restaurantId, from, to);

            return rows.Select(x => MapToDto(x.Report, x.OpeningCashAmount)).ToList();
        }

        public async Task<List<ShiftReportDto>> GetShiftReportsByStaffAsync(Guid staffId)
        {
            var rows = await _unitOfWork.ShiftReports.GetReportsByStaffAsync(staffId);

            return rows.Select(x => MapToDto(x.Report, x.OpeningCashAmount)).ToList();
        }

        private static ShiftReportDto MapToDto(Domain.Entities.Shifts.ShiftReport r, decimal openingCash) => new()
        {
            Id = r.Id,
            ShiftId = r.ShiftId,
            ReportDate = r.ReportDate,
            TotalCashOrder = r.TotalCashOrder,
            TotalTransferOrder = r.TotalTransferOrder,
            TotalRefundAmount = r.TotalRefundAmount,
            ExpectedCashAmount = r.ExpectedCashAmount,
            ActualCashAmount = r.ActualCashAmount,
            Difference = r.Difference,
            ExpectedTotalAmount = openingCash + r.TotalCashOrder + r.TotalTransferOrder,
            Note = r.Note
        };
    }
}

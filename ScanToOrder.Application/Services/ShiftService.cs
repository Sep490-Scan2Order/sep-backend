using AutoMapper;
using ScanToOrder.Application.DTOs.Other;
using ScanToOrder.Application.DTOs.Shift;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Entities.Shifts;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Domain.Entities.Orders;

namespace ScanToOrder.Application.Services
{
    public class ShiftService : IShiftService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IRealtimeService _realtimeService;
        public ShiftService(IUnitOfWork unitOfWork, IMapper mapper, IRealtimeService realtimeService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _realtimeService = realtimeService;
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
            await _realtimeService.NotifyShiftChanged(shift.StaffId.ToString(), _mapper.Map<ShiftDto>(shift));

            return _mapper.Map<ShiftDto>(shift);
        }

        public async Task<ShiftDto> CheckOutShiftAsync(int shiftId, decimal actualCashAmount, string? note)
        {
            var shift = await GetAndValidateOpenShiftAsync(shiftId);
            var transactions = await GetSuccessfulTransactionsAsync(shiftId);
            var metrics = CalculateShiftMetrics(transactions);

            await PerformCheckOutTransitionAsync(shift, actualCashAmount, metrics, note);

            return _mapper.Map<ShiftDto>(shift);
        }

        private async Task<Shift> GetAndValidateOpenShiftAsync(int shiftId)
        {
            var shift = await _unitOfWork.Shifts.GetByIdAsync(shiftId);
            if (shift == null)
                throw new DomainException(Message.ShiftMessage.ShiftError.SHIFT_NOT_FOUND);

            if (shift.Status != ShiftStatus.Open)
                throw new DomainException(Message.ShiftMessage.ShiftError.SHIFT_ALREADY_CLOSED);

            return shift;
        }

        private async Task<List<Transaction>> GetSuccessfulTransactionsAsync(int shiftId)
        {
            var transactions = await _unitOfWork.Transactions
                .FindAsync(t => t.ShiftId == shiftId && t.Status == OrderTransactionStatus.Success);
            return transactions.ToList();
        }

        private static ShiftMetrics CalculateShiftMetrics(List<Transaction> transactions)
        {
            decimal cashPayments = transactions
                .Where(t => t.PaymentMethod == PaymentMethod.Cash && t.TransactionType == TransactionType.Payment)
                .Sum(t => t.TotalAmount);

            decimal cashRefunds = transactions
                .Where(t => t.PaymentMethod == PaymentMethod.Cash && t.TransactionType == TransactionType.Refund)
                .Sum(t => t.TotalAmount);

            decimal transferPayments = transactions
                .Where(t => t.PaymentMethod == PaymentMethod.BankTransfer && t.TransactionType == TransactionType.Payment)
                .Sum(t => t.TotalAmount);

            decimal transferRefunds = transactions
                .Where(t => t.PaymentMethod == PaymentMethod.BankTransfer && t.TransactionType == TransactionType.Refund)
                .Sum(t => t.TotalAmount);

            return new ShiftMetrics(
                TotalCashOrder: cashPayments - cashRefunds,
                TotalTransferOrder: transferPayments - transferRefunds,
                TotalRefundAmount: cashRefunds + transferRefunds
            );
        }

        private async Task PerformCheckOutTransitionAsync(Shift shift, decimal actualCashAmount, ShiftMetrics metrics, string? note)
        {
            await using var tx = await _unitOfWork.BeginTransactionAsync();
            try
            {
                shift.EndDate = DateTime.UtcNow;
                shift.Status = ShiftStatus.Closed;
                shift.Note = note ?? string.Empty;
                _unitOfWork.Shifts.Update(shift);

                decimal expectedCash = shift.OpeningCashAmount + metrics.TotalCashOrder;
                decimal difference = actualCashAmount - expectedCash;

                var report = new ShiftReport
                {
                    ShiftId = shift.Id,
                    ReportDate = DateTime.UtcNow,
                    TotalCashOrder = metrics.TotalCashOrder,
                    TotalTransferOrder = metrics.TotalTransferOrder,
                    TotalRefundAmount = metrics.TotalRefundAmount,
                    ExpectedCashAmount = expectedCash,
                    ActualCashAmount = actualCashAmount,
                    Difference = difference,
                    Note = note ?? string.Empty
                };

                await _unitOfWork.ShiftReports.AddAsync(report);
                await _unitOfWork.SaveAsync();
                await tx.CommitAsync();

                await _realtimeService.NotifyShiftChanged(shift.StaffId.ToString(), _mapper.Map<ShiftDto>(shift));
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
                
            var staff = await _unitOfWork.Staffs.GetByIdAsync(shift.StaffId);

            var report = await _unitOfWork.ShiftReports
                .FirstOrDefaultAsync(r => r.ShiftId == shiftId);

            if (report == null)
                throw new DomainException(Message.ShiftMessage.ShiftError.SHIFT_REPORT_NOT_FOUND);

            return _mapper.Map<ShiftReportDto>((report, shift.OpeningCashAmount, staff?.Name ?? string.Empty));
        }

        public async Task<PagedResult<ShiftReportDto>> GetAllShiftReportsAsync(int restaurantId, int pageIndex, int pageSize, DateTime? from, DateTime? to)
        {
            var result = await _unitOfWork.ShiftReports
                .GetReportsByRestaurantAsync(restaurantId, from, to, pageIndex, pageSize);

            return new PagedResult<ShiftReportDto>
            {
                Items = result.Items.Select(x => _mapper.Map<ShiftReportDto>(x)),
                TotalCount = result.TotalCount,
                Page = pageIndex,
                PageSize = pageSize
            };
        }

        public async Task<PagedResult<ShiftReportDto>> GetShiftReportsByStaffAsync(Guid staffId, int pageIndex, int pageSize)
        {
            var result = await _unitOfWork.ShiftReports.GetReportsByStaffAsync(staffId, pageIndex, pageSize);

            return new PagedResult<ShiftReportDto>
            {
                Items = result.Items.Select(x => _mapper.Map<ShiftReportDto>(x)),
                TotalCount = result.TotalCount,
                Page = pageIndex,
                PageSize = pageSize
            };
        }

        public  async Task<ShiftDto>  GetShiftByIdAsync(Guid staffId)
        {
            var shift = await _unitOfWork.Shifts.GetCurrentShiftByStaffIdAsync(staffId);
            if (shift == null)
                throw new DomainException(Message.ShiftMessage.ShiftError.SHIFT_NOT_FOUND);

            return _mapper.Map<ShiftDto>(shift);
        }

        private record ShiftMetrics(
            decimal TotalCashOrder,
            decimal TotalTransferOrder,
            decimal TotalRefundAmount
        );
    }
}

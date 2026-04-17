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
        private readonly IAuthenticatedUserService _authenticatedUserService;
        public ShiftService(IUnitOfWork unitOfWork, IMapper mapper, IRealtimeService realtimeService, IAuthenticatedUserService authenticatedUserService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _realtimeService = realtimeService;
            _authenticatedUserService = authenticatedUserService;
        }

        public async Task<ShiftDto> CheckInShiftAsync(int restaurantId, Guid staffId, decimal openingCashAmount, string? note)
        {
            var restaurant = await _unitOfWork.Restaurants.GetByIdAsync(restaurantId);

            if (restaurant == null)
            {
                throw new DomainException(Message.RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);
            }

            // Xác định vai trò
            Enum.TryParse<Role>(_authenticatedUserService.Role, out var userRole);
            var isCashier = userRole == Role.Cashier;

            var activeCashierShift = await _unitOfWork.Shifts.GetActiveCashierShiftAsync(restaurantId);

            // Kiểm tra xem nhân viên này đã có ca làm nào đang mở chưa
            var existingOpenShift = await _unitOfWork.Shifts.GetCurrentShiftByStaffIdAsync(staffId);
            if (existingOpenShift != null)
            {
                throw new DomainException(Message.ShiftMessage.ShiftError.SHIFT_ALREADY_OPEN);
            }

            if (isCashier)
            {
                if (openingCashAmount < restaurant.MinCashAmount)
                {
                    throw new DomainException(Message.ShiftMessage.ShiftError.OPENING_CASH_INVALID);
                }

                if (activeCashierShift != null)
                {
                    throw new DomainException(Message.ShiftMessage.ShiftError.SHIFT_ALREADY_OPEN);
                }
            }
            else
            {
                if (activeCashierShift == null)
                {
                    throw new DomainException(Message.ShiftMessage.ShiftError.CASHIER_SHIFT_NOT_OPEN);
                }
            }

            var shift = new Shift
            {
                RestaurantId = restaurantId,
                StaffId = staffId,
                StartDate = DateTime.UtcNow,
                OpeningCashAmount = isCashier ? openingCashAmount : 0,
                Note = note ?? string.Empty,
                Status = ShiftStatus.Open,
                Type = isCashier ? ShiftType.Cashier : ShiftType.Staff,
                ParentShiftId = isCashier ? null : activeCashierShift?.Id
            };

            await _unitOfWork.Shifts.AddAsync(shift);
            await _unitOfWork.SaveAsync();
            await _realtimeService.NotifyShiftChanged(shift.StaffId.ToString(), _mapper.Map<ShiftDto>(shift));

            return _mapper.Map<ShiftDto>(shift);
        }

        public async Task<ShiftDto> CheckOutShiftAsync(int shiftId, decimal actualCashAmount, string? note)
        {
            var shift = await GetAndValidateOpenShiftAsync(shiftId);

            if (shift.Type == ShiftType.Cashier)
            {
                var restaurant = await _unitOfWork.Restaurants.GetByIdAsync(shift.RestaurantId);
                if (restaurant != null && actualCashAmount < restaurant.MinCashAmount)
                {
                    throw new DomainException(Message.ShiftMessage.ShiftError.CASH_AMOUNT_INVALID);
                }

                var hasOpenStaff = await _unitOfWork.Shifts.HasOpenSubShiftsAsync(shiftId);
                if (hasOpenStaff)
                {
                    throw new DomainException(Message.ShiftMessage.ShiftError.STAFF_MUST_CHECKOUT_FIRST);
                }

                var transactions = await GetSuccessfulTransactionsAsync(shiftId);
                var metrics = CalculateShiftMetrics(transactions);

                await PerformCheckOutTransitionAsync(shift, actualCashAmount, metrics, note);
            }
            else
            {
                // Đối với nhân viên (Staff), chỉ cần đóng trạng thái
                shift.EndDate = DateTime.UtcNow;
                shift.Status = ShiftStatus.Closed;
                shift.Note = note ?? string.Empty;
                _unitOfWork.Shifts.Update(shift);
                await _unitOfWork.SaveAsync();
                await _realtimeService.NotifyShiftChanged(shift.StaffId.ToString(), _mapper.Map<ShiftDto>(shift));
            }

            return _mapper.Map<ShiftDto>(shift);
        }

        public async Task BlockStaffShiftAsync(int shiftId, string reason)
        {
            var shift = await _unitOfWork.Shifts.GetByIdAsync(shiftId);
            if (shift == null)
                throw new DomainException(Message.ShiftMessage.ShiftError.SHIFT_NOT_FOUND);

            if (shift.Type != ShiftType.Staff)
                throw new DomainException(Message.ShiftMessage.ShiftError.UNAUTHORIZED_ACCESS);

            shift.Status = ShiftStatus.Blocked;
            shift.EndDate = DateTime.UtcNow;
            shift.Note = reason;
            
            _unitOfWork.Shifts.Update(shift);
            await _unitOfWork.SaveAsync();
            await _realtimeService.NotifyShiftChanged(shift.StaffId.ToString(), _mapper.Map<ShiftDto>(shift));
        }

        public async Task<IEnumerable<ShiftDto>> GetStaffShiftsByCashierShiftIdAsync(int cashierShiftId)
        {
            var shifts = await _unitOfWork.Shifts.GetOpenSubShiftsByParentIdAsync(cashierShiftId);
            return _mapper.Map<IEnumerable<ShiftDto>>(shifts);
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
            return await _unitOfWork.Transactions.GetSuccessfulTransactionsByShiftIdAsync(shiftId);
        }

        private static ShiftMetrics CalculateShiftMetrics(List<Transaction> transactions)
        {
            // Chỉ lấy các giao dịch Thanh toán của đơn hàng đã Served
            var servedPayments = transactions
                .Where(t => t.TransactionType == TransactionType.Payment && t.Order.Status == OrderStatus.Served)
                .ToList();

            decimal totalCash = servedPayments
                .Where(t => t.PaymentMethod == PaymentMethod.Cash)
                .Sum(t => t.Order.FinalAmount);

            decimal totalTransfer = servedPayments
                .Where(t => t.PaymentMethod == PaymentMethod.BankTransfer)
                .Sum(t => t.Order.FinalAmount);

            // Tiền hoàn vẫn tính từ Transaction để hiển thị thông tin
            decimal totalRefund = transactions
                .Where(t => t.TransactionType == TransactionType.Refund)
                .Sum(t => t.TotalAmount);

            return new ShiftMetrics
            {
                TotalCashOrder = totalCash,
                TotalTransferOrder = totalTransfer,
                TotalRefundAmount = totalRefund
            };
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

            var report = await _unitOfWork.ShiftReports.GetReportByShiftIdAsync(shiftId);

            if (report == null)
                throw new DomainException(Message.ShiftMessage.ShiftError.SHIFT_REPORT_NOT_FOUND);

            return MapToShiftReportDto(report, shift.OpeningCashAmount, staff?.Name ?? string.Empty);
        }

        public async Task<ShiftReportDto> GetShiftPreviewAsync(int shiftId)
        {
            var shift = await GetAndValidateOpenShiftAsync(shiftId);
            var staff = await _unitOfWork.Staffs.GetByIdAsync(shift.StaffId);

            var transactions = await GetSuccessfulTransactionsAsync(shiftId);
            var filteredTransactions = transactions.Where(t => 
                (t.TransactionType == TransactionType.Payment && t.Order.Status == OrderStatus.Served) || 
                (t.TransactionType == TransactionType.Refund)
            ).ToList();
            var metrics = CalculateShiftMetrics(filteredTransactions);

            decimal expectedCash = shift.OpeningCashAmount + metrics.TotalCashOrder;

            return new ShiftReportDto
            {
                Id = 0,
                ShiftId = shift.Id,
                ReportDate = DateTime.UtcNow,
                TotalCashOrder = metrics.TotalCashOrder,
                TotalTransferOrder = metrics.TotalTransferOrder,
                TotalRefundAmount = metrics.TotalRefundAmount,
                ExpectedCashAmount = expectedCash,
                ActualCashAmount = 0,
                Difference = 0,
                Note = shift.Note ?? string.Empty,
                ExpectedTotalAmount = shift.OpeningCashAmount + metrics.TotalCashOrder + metrics.TotalTransferOrder,
                CashierName = staff?.Name ?? string.Empty
            };
        }

        public async Task<PagedResult<ShiftReportDto>> GetAllShiftReportsAsync(int restaurantId, int pageIndex, int pageSize, DateTime? from, DateTime? to)
        {
            var result = await _unitOfWork.ShiftReports
                .GetReportsByRestaurantAsync(restaurantId, from, to, pageIndex, pageSize);

            return new PagedResult<ShiftReportDto>
            {
                Items = result.Items.Select(x => MapToShiftReportDto(x.Report, x.OpeningCashAmount, x.CashierName)),
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
                Items = result.Items.Select(x => MapToShiftReportDto(x.Report, x.OpeningCashAmount, x.CashierName)),
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

        private static ShiftReportDto MapToShiftReportDto(ShiftReport report, decimal openingCashAmount, string cashierName)
        {
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
                Note = report.Note,
                ExpectedTotalAmount = openingCashAmount + report.TotalCashOrder + report.TotalTransferOrder,
                CashierName = cashierName
            };
        }
    }
}

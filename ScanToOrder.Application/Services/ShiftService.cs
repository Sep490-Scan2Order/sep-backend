using AutoMapper;
using ScanToOrder.Application.DTOs.Other;
using ScanToOrder.Application.DTOs.Shift;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Entities.Shifts;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Domain.Entities.Orders;

using ScanToOrder.Application.Utils;

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

        public async Task<ShiftDto> CheckInShiftAsync(int restaurantId, Guid staffId, string? note)
        {
            var restaurant = await _unitOfWork.Restaurants.GetByIdAsync(restaurantId);

            if (restaurant == null)
            {
                throw new DomainException(Message.RestaurantMessage.RestaurantError.RESTAURANT_NOT_FOUND);
            }

            Enum.TryParse<Role>(_authenticatedUserService.Role, out var userRole);
            var isCashier = userRole == Role.Cashier;

            var activeCashierShift = await _unitOfWork.Shifts.GetActiveCashierShiftAsync(restaurantId);

            var existingOpenShift = await _unitOfWork.Shifts.GetCurrentShiftByStaffIdAsync(staffId);
            if (existingOpenShift != null)
            {
                throw new DomainException(Message.ShiftMessage.ShiftError.SHIFT_ALREADY_OPEN);
            }

            if (isCashier)
            {
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
                Note = note ?? string.Empty,
                Status = ShiftStatus.Open,
                Type = isCashier ? ShiftType.Cashier : ShiftType.Staff,
                ParentShiftId = isCashier ? null : activeCashierShift?.Id
            };

            await _unitOfWork.Shifts.AddAsync(shift);
            await _unitOfWork.SaveAsync();
            await _realtimeService.NotifyShiftChanged(shift.StaffId.ToString(), _mapper.Map<ShiftDto>(shift));

            if (shift.ParentShiftId.HasValue)
            {
                var parentShift = await _unitOfWork.Shifts.GetByIdAsync(shift.ParentShiftId.Value);
                if (parentShift != null)
                {
                    await _realtimeService.NotifyShiftChanged(parentShift.StaffId.ToString(), _mapper.Map<ShiftDto>(shift));
                }
            }

            return _mapper.Map<ShiftDto>(shift);
        }

        public async Task<ShiftDto> CheckOutShiftAsync(int shiftId, string? note)
        {
            var shift = await GetAndValidateOpenShiftAsync(shiftId);

            if (shift.Type == ShiftType.Cashier)
            {
                var restaurant = await _unitOfWork.Restaurants.GetByIdAsync(shift.RestaurantId);

                var hasOpenStaff = await _unitOfWork.Shifts.HasOpenSubShiftsAsync(shiftId);
                if (hasOpenStaff)
                {
                    throw new DomainException(Message.ShiftMessage.ShiftError.STAFF_MUST_CHECKOUT_FIRST);
                }

                var transactions = await GetSuccessfulTransactionsAsync(shiftId);
                var metrics = CalculateShiftMetrics(transactions);

                await PerformCheckOutTransitionAsync(shift, metrics, note);
            }
            else
            {
                shift.EndDate = DateTime.UtcNow;
                shift.Status = ShiftStatus.Closed;
                shift.Note = note ?? string.Empty;
                _unitOfWork.Shifts.Update(shift);
                await _unitOfWork.SaveAsync();
                await _realtimeService.NotifyShiftChanged(shift.StaffId.ToString(), _mapper.Map<ShiftDto>(shift));

                if (shift.ParentShiftId.HasValue)
                {
                    var parentShift = await _unitOfWork.Shifts.GetByIdAsync(shift.ParentShiftId.Value);
                    if (parentShift != null)
                    {
                        await _realtimeService.NotifyShiftChanged(parentShift.StaffId.ToString(), _mapper.Map<ShiftDto>(shift));
                    }
                }
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

            if (shift.ParentShiftId.HasValue)
            {
                var parentShift = await _unitOfWork.Shifts.GetByIdAsync(shift.ParentShiftId.Value);
                if (parentShift != null)
                {
                    await _realtimeService.NotifyShiftChanged(parentShift.StaffId.ToString(), _mapper.Map<ShiftDto>(shift));
                }
            }
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
            var servedPayments = transactions
                .Where(t => t.TransactionType == TransactionType.Payment && t.Order.Status == OrderStatus.Served)
                .ToList();

            decimal totalCash = servedPayments
                .Where(t => t.PaymentMethod == PaymentMethod.Cash)
                .Sum(t => t.Order.FinalAmount);

            decimal totalTransfer = servedPayments
                .Where(t => t.PaymentMethod == PaymentMethod.BankTransfer)
                .Sum(t => t.Order.FinalAmount);

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

        private async Task PerformCheckOutTransitionAsync(Shift shift, ShiftMetrics metrics, string? note)
        {
            await using var tx = await _unitOfWork.BeginTransactionAsync();
            try
            {
                shift.EndDate = DateTime.UtcNow;
                shift.Status = ShiftStatus.Closed;
                shift.Note = note ?? string.Empty;
                _unitOfWork.Shifts.Update(shift);

                decimal totalTransferred = shift.ShiftTransfers.Sum(t => t.Amount);
                decimal difference = totalTransferred - metrics.TotalCashOrder;

                var report = new ShiftReport
                {
                    ShiftId = shift.Id,
                    ReportDate = DateTime.UtcNow,
                    TotalCashOrder = metrics.TotalCashOrder,
                    TotalTransferOrder = metrics.TotalTransferOrder,
                    TotalRefundAmount = metrics.TotalRefundAmount,
                    ActualCashAmount = totalTransferred,
                    Difference = difference,
                    IsTransferred = (difference == 0),
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

            return MapToShiftReportDto(report, staff?.Name ?? string.Empty);
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

            decimal totalTransferred = shift.ShiftTransfers.Sum(t => t.Amount);
            decimal difference = totalTransferred - metrics.TotalCashOrder;

            return new ShiftReportDto
            {
                Id = 0,
                ShiftId = shift.Id,
                ReportDate = DateTime.UtcNow,
                TotalCashOrder = metrics.TotalCashOrder,
                TotalTransferOrder = metrics.TotalTransferOrder,
                TotalRefundAmount = metrics.TotalRefundAmount,
                ActualCashAmount = totalTransferred,
                Difference = difference,
                IsTransferred = (difference == 0),
                Note = shift.Note ?? string.Empty,
                ExpectedTotalAmount = metrics.TotalCashOrder + metrics.TotalTransferOrder,
                CashierName = staff?.Name ?? string.Empty
            };
        }

        public async Task<PagedResult<ShiftReportDto>> GetAllShiftReportsAsync(int restaurantId, int pageIndex, int pageSize, DateTime? from, DateTime? to)
        {
            var result = await _unitOfWork.ShiftReports
                .GetReportsByRestaurantAsync(restaurantId, from, to, pageIndex, pageSize);

            return new PagedResult<ShiftReportDto>
            {
                Items = result.Items.Select(x => MapToShiftReportDto(x.Report, x.CashierName)),
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
                Items = result.Items.Select(x => MapToShiftReportDto(x.Report, x.CashierName)),
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

        private static ShiftReportDto MapToShiftReportDto(ShiftReport report, string cashierName)
        {
            return new ShiftReportDto
            {
                Id = report.Id,
                ShiftId = report.ShiftId,
                ReportDate = report.ReportDate,
                TotalCashOrder = report.TotalCashOrder,
                TotalTransferOrder = report.TotalTransferOrder,
                TotalRefundAmount = report.TotalRefundAmount,
                ActualCashAmount = report.ActualCashAmount,
                Difference = report.Difference,
                IsTransferred = report.IsTransferred,
                Note = report.Note,
                ExpectedTotalAmount = report.TotalCashOrder + report.TotalTransferOrder,
                CashierName = cashierName
            };
        }
        public async Task<ShiftTransferQrResponse> GetTransferQrAsync(int shiftId)
        {
            var shift = await _unitOfWork.Shifts.GetByIdAsync(shiftId);
            if (shift == null)
                throw new DomainException(Message.ShiftMessage.ShiftError.SHIFT_NOT_FOUND);

            var restaurant = await _unitOfWork.Restaurants.GetByIdWithTenantBankAsync(shift.RestaurantId);
            if (restaurant?.Tenant?.Bank == null || string.IsNullOrWhiteSpace(restaurant.Tenant.CardNumber))
                throw new DomainException(Message.OrderMessage.OrderError.RESTAURANT_NO_BANK_CONFIGURED);

            var transactions = await GetSuccessfulTransactionsAsync(shiftId);
            var metrics = CalculateShiftMetrics(transactions);
            var allSuccessTransfers = await _unitOfWork.ShiftTransfers
                .FindAsync(t => t.ShiftId == shiftId && t.Status == ShiftTransferStatus.Success);
            var alreadyTransferred = allSuccessTransfers.Sum(t => t.Amount);
            var amountToTransfer = metrics.TotalCashOrder - alreadyTransferred;

            if (amountToTransfer <= 0)
                throw new DomainException("Ca làm việc đã nộp đủ doanh thu tiền mặt.");

            var (qrUrl, paymentCode) = BankQrLinkUtils.GenerateSePayQrUrl(
                restaurant.Tenant.CardNumber,
                restaurant.Tenant.Bank.ShortName,
                amountToTransfer,
                PaymentIntent.ShiftPayment
            );

            var transfer = new ShiftTransfer
            {
                ShiftId = shiftId,
                Amount = amountToTransfer,
                TransactionCode = paymentCode,
                Status = ShiftTransferStatus.Pending,
                Note = $"Nộp tiền ca làm việc #{shiftId}"
            };

            await _unitOfWork.ShiftTransfers.AddAsync(transfer);
            await _unitOfWork.SaveAsync();

            return new ShiftTransferQrResponse
            {
                QrUrl = qrUrl,
                PaymentCode = paymentCode,
                Amount = amountToTransfer,
                Note = transfer.Note
            };
        }

        public async Task HandleShiftTransferWebhookAsync(string paymentCode, decimal amount)
        {
            var transfer = await _unitOfWork.ShiftTransfers.FirstOrDefaultAsync(t => t.TransactionCode.ToLower() == paymentCode.ToLower());
            if (transfer == null) return;

            if (transfer.Status == ShiftTransferStatus.Success) return;


            transfer.Status = ShiftTransferStatus.Success;
            _unitOfWork.ShiftTransfers.Update(transfer);

            var report = await _unitOfWork.ShiftReports.GetReportByShiftIdAsync(transfer.ShiftId);
            if (report != null)
            {
                var allSuccessTransfers = await _unitOfWork.ShiftTransfers
                    .FindAsync(t => t.ShiftId == transfer.ShiftId && t.Status == ShiftTransferStatus.Success);
                
                report.ActualCashAmount = allSuccessTransfers.Sum(t => t.Amount) + amount;
                
                if (report.ActualCashAmount >= report.TotalCashOrder)
                {
                    report.IsTransferred = true;
                }
                
                report.Difference = report.ActualCashAmount - report.TotalCashOrder;
                _unitOfWork.ShiftReports.Update(report);

                if (report.IsTransferred)
                {
                    await _realtimeService.NotifyShiftTransferSuccess(report.Shift.StaffId.ToString(), transfer.ShiftId);
                }
            }

            await _unitOfWork.SaveAsync();
        }

        public async Task<IEnumerable<ShiftReportDto>> GetPendingShiftReportsAsync(Guid staffId)
        {
            var reports = await _unitOfWork.ShiftReports.FindAsync(r => 
                r.Shift.StaffId == staffId && 
                r.Shift.Status == ShiftStatus.Closed && 
                !r.IsTransferred);

            var staff = await _unitOfWork.Staffs.GetByIdAsync(staffId);
            var cashierName = staff?.Name ?? string.Empty;

            return reports
                .OrderByDescending(r => r.ReportDate)
                .Select(r => MapToShiftReportDto(r, cashierName));
        }
    }
}

using ScanToOrder.Application.DTOs.Orders;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Utils;
using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ScanToOrder.Application.Services
{
    public class RefundService : IRefundService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<RefundService> _logger;
        private readonly IStorageService _storageService;

        public RefundService(IUnitOfWork unitOfWork, ILogger<RefundService> logger, IStorageService storageService)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _storageService = storageService;
        }

        public async Task<bool> ConfirmSystemErrorPaymentAsync(ConfirmSystemPaymentRequest request)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(request.OrderId);
            if (order == null)
            {
                throw new DomainException(OrderMessage.OrderError.ORDER_NOT_FOUND);
            }

            if (order.Status != OrderStatus.Unpaid)
            {
                throw new DomainException(OrderMessage.OrderError.ONLY_CONFIRM_UNPAID_ORDER);
            }

            // Xử lý upload ảnh minh chứng
            string? paymentProofUrl = null;
            if (request.ImageFile != null && request.ImageFile.Length > 0)
            {
                try
                {
                    using var ms = new MemoryStream();
                    await request.ImageFile.CopyToAsync(ms);
                    var fileBytes = ms.ToArray();
                    string extension = Path.GetExtension(request.ImageFile.FileName);
                    string fileName = $"proof_{order.OrderCode}_{Guid.NewGuid()}{extension}";
                    paymentProofUrl = await _storageService.UploadPaymentProofAsync(fileBytes, fileName);
                }
                catch (Exception ex)
                {
                    throw new DomainException($"Lỗi khi tải ảnh minh chứng lên: {ex.Message}");
                }
            }

            await using var tx = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // 1. Cập nhật trạng thái đơn hàng sang Pending (đã thanh toán, chờ xử lý)
                order.Status = OrderStatus.Pending;
                order.PaymentProofUrl = paymentProofUrl;
                order.ResponsibleStaffId = request.ResponsibleStaffId;
                order.RefundType = RefundType.SystemError;
                order.Note = request.Note;
                _unitOfWork.Orders.Update(order);

                // 2. Tạo giao dịch thành công để ghi nhận doanh thu vào hệ thống
                var activeShift = await _unitOfWork.Shifts.FirstOrDefaultAsync(
                    s => s.RestaurantId == order.RestaurantId && s.Status == ShiftStatus.Open);

                var transaction = new Transaction
                {
                    OrderId = order.Id,
                    TotalAmount = order.FinalAmount,
                    PaymentMethod = PaymentMethod.BankTransfer,
                    Status = OrderTransactionStatus.Success,
                    ShiftId = activeShift?.Id
                    // Note = "Xác nhận thủ công do lỗi hệ thống" (Bỏ Note theo yêu cầu trước đó)
                };
                await _unitOfWork.Transactions.AddAsync(transaction);

                await _unitOfWork.SaveAsync();
                await tx.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi xác nhận thanh toán hệ thống cho Order {OrderId}", request.OrderId);
                throw;
            }
        }
        public async Task<bool> RefundOrderAsync(RefundRequest request)
        {
            var originalOrder = await _unitOfWork.Orders.GetByIdAsync(request.OrderId);
            if (originalOrder == null)
            {
                throw new DomainException(OrderMessage.OrderError.ORDER_NOT_FOUND);
            }

            // Chỉ hoàn tiền cho những đơn đã thanh toán/phục vụ
            if (originalOrder.Status == OrderStatus.Cancelled)
            {
                throw new DomainException(OrderMessage.OrderError.ORDER_ALREADY_CANCELLED_OR_REFUNDED);
            }

            await using var tx = await _unitOfWork.BeginTransactionAsync();
            try
            {
                // 1. Chuyển trạng thái đơn gốc sang Cancelled để đối trừ doanh thu
                originalOrder.Status = OrderStatus.Cancelled;
                _unitOfWork.Orders.Update(originalOrder);

                // 2. Xử lý Log và Giao dịch tùy theo loại lỗi
                if (request.RefundType == RefundType.SystemError)
                {
                    throw new DomainException(OrderMessage.OrderError.SYSTEM_ERROR_MANUAL_ONLY);
                }

                // Tạo Đơn hàng 0đ làm log cho lỗi Khách quan hoặc Nhân viên
                var (startUtc, endUtc, dateInt) = GetVietnamDayRangeUtc();
                int nextOrderCode = await _unitOfWork.Orders.GetNextDailyOrderCodeAsync(
                    originalOrder.RestaurantId, startUtc, endUtc, dateInt);

                var refundOrder = new Order
                {
                    Id = Guid.NewGuid(),
                    RestaurantId = originalOrder.RestaurantId,
                    OrderCode = nextOrderCode,
                    RefundOrderId = originalOrder.Id,
                    RefundType = request.RefundType,
                    ResponsibleStaffId = request.ResponsibleStaffId,
                    typeOrder = TypeOrder.Refund,
                    Status = OrderStatus.Served,
                    FinalAmount = 0,
                    TotalAmount = 0,
                    PromotionDiscount = 0,
                    Note = request.Note,
                    NumberPhone = originalOrder.NumberPhone,
                    QrCodeUrl = "REFUND_LOG",
                    Type = originalOrder.Type,
                    IsPreOrder = false,
                    IsScanned = true
                };

                await _unitOfWork.Orders.AddAsync(refundOrder);

                // CHỈ log giao dịch âm cho lỗi Khách quan (Objective) và trả tiền mặt
                if (request.RefundType == RefundType.Objective && originalOrder.Type == "Cash" )
                {
                    var activeShift = await _unitOfWork.Shifts.FirstOrDefaultAsync(
                        s => s.RestaurantId == originalOrder.RestaurantId && s.Status == ShiftStatus.Open);

                    var transaction = new Transaction
                    {
                        OrderId = refundOrder.Id,
                        TotalAmount = -originalOrder.FinalAmount,
                        PaymentMethod = PaymentMethod.Cash,
                        Status = OrderTransactionStatus.Success,
                        ShiftId = activeShift?.Id
                    };
                    await _unitOfWork.Transactions.AddAsync(transaction);
                }

                await _unitOfWork.SaveAsync();
                await tx.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi xử lý hoàn tiền cho Order {OrderId}", request.OrderId);
                throw;
            }
        }

        private static (DateTime StartUtc, DateTime EndUtc, int DateInt) GetVietnamDayRangeUtc()
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                var nowVn = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
                var vnDate = nowVn.Date;
                var startUtc = TimeZoneInfo.ConvertTimeToUtc(vnDate, tz);
                var endUtc = TimeZoneInfo.ConvertTimeToUtc(vnDate.AddDays(1), tz);
                int dateInt = (vnDate.Year * 10000) + (vnDate.Month * 100) + vnDate.Day;
                return (startUtc, endUtc, dateInt);
            }
            catch
            {
                var utcDate = DateTime.UtcNow.Date;
                int dateInt = (utcDate.Year * 10000) + (utcDate.Month * 100) + utcDate.Day;
                return (utcDate, utcDate.AddDays(1), dateInt);
            }
        }
    }
}

using ScanToOrder.Application.DTOs.Orders;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Utils;
using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using AutoMapper;

namespace ScanToOrder.Application.Services
{
    public class RefundService : IRefundService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<RefundService> _logger;
        private readonly IStorageService _storageService;
        private readonly IRealtimeService _realtimeService;
        private readonly IMapper _mapper;

        public RefundService(
            IUnitOfWork unitOfWork, 
            ILogger<RefundService> logger, 
            IStorageService storageService,
            IRealtimeService realtimeService,
            IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _storageService = storageService;
            _realtimeService = realtimeService;
            _mapper = mapper;
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
                order.Status = OrderStatus.Pending;
                order.PaymentProofUrl = paymentProofUrl;
                order.ResponsibleStaffId = request.ResponsibleStaffId;
                order.RefundType = RefundType.SystemError;
                order.Note = request.Note;
                _unitOfWork.Orders.Update(order);

                var activeShift = await _unitOfWork.Shifts.FirstOrDefaultAsync(
                    s => s.RestaurantId == order.RestaurantId && s.Status == ShiftStatus.Open);

                var transaction = new Transaction
                {
                    OrderId = order.Id,
                    TotalAmount = order.FinalAmount,
                    PaymentMethod = PaymentMethod.BankTransfer,
                    Status = OrderTransactionStatus.Success,
                    ShiftId = activeShift?.Id
                };
                await _unitOfWork.Transactions.AddAsync(transaction);

                await _unitOfWork.SaveAsync();
                await tx.CommitAsync();

                try
                {
                    await _realtimeService.NotifyOrderStatusChanged(order.RestaurantId.ToString(), order.Id.ToString(), (int)order.Status);
                    await _realtimeService.NotifyCustomerOrderStatusChanged(order.Id.ToString(), (int)order.Status);

                    string audioUrl = await _storageService.GetOrGeneratePaymentReceivedAudioAsync(order.OrderCode, order.FinalAmount);
                    await _realtimeService.NotifyPaymentReceived(order.RestaurantId.ToString(), order.OrderCode, order.FinalAmount, audioUrl);

                    var orderWithDetails = await _unitOfWork.Orders.GetOrderWithDetailsForKdsAsync(order.Id);
                    if (orderWithDetails != null)
                    {
                        var realtimeDto = _mapper.Map<OrderRealtimeDto>(orderWithDetails);
                        await _realtimeService.SendOrderToKitchen(order.RestaurantId.ToString(), realtimeDto);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Lỗi SignalR: {Message}", ex.Message);
                }

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

            if (originalOrder.Status == OrderStatus.Cancelled)
            {
                throw new DomainException(OrderMessage.OrderError.ORDER_ALREADY_CANCELLED_OR_REFUNDED);
            }

            await using var tx = await _unitOfWork.BeginTransactionAsync();
            try
            {
                originalOrder.Status = OrderStatus.Cancelled;
                _unitOfWork.Orders.Update(originalOrder);

                if (request.RefundType == RefundType.Objective && (request.ImageFile == null || request.ImageFile.Length == 0))
                {
                    throw new DomainException("Trường hợp khách quan bắt buộc phải có ảnh minh chứng chuyển khoản.");
                }

                string? paymentProofUrl = null;
                if (request.ImageFile != null && request.ImageFile.Length > 0)
                {
                    try
                    {
                        using var ms = new MemoryStream();
                        await request.ImageFile.CopyToAsync(ms);
                        var fileBytes = ms.ToArray();
                        string extension = Path.GetExtension(request.ImageFile.FileName);
                        string fileName = $"refund_proof_{originalOrder.OrderCode}_{Guid.NewGuid()}{extension}";
                        paymentProofUrl = await _storageService.UploadPaymentProofAsync(fileBytes, fileName);
                    }
                    catch (Exception ex)
                    {
                        throw new DomainException($"Lỗi khi tải ảnh minh chứng hoàn tiền lên: {ex.Message}");
                    }
                }

                var refundOrder = new Order
                {
                    Id = Guid.NewGuid(),
                    RestaurantId = originalOrder.RestaurantId,
                    OrderCode = originalOrder.OrderCode,
                    RefundOrderId = originalOrder.Id,
                    RefundType = request.RefundType,
                    ResponsibleStaffId = request.ResponsibleStaffId,
                    typeOrder = TypeOrder.Refund,
                    Status = OrderStatus.Served,
                    NumberPhone = "",
                    FinalAmount = 0,
                    TotalAmount = 0,
                    PromotionDiscount = 0,
                    Note = request.Note,
                    QrCodeUrl = "REFUND_LOG",
                    Type = originalOrder.Type,
                    IsPreOrder = false,
                    IsScanned = true,
                    PaymentProofUrl = paymentProofUrl
                };

                await _unitOfWork.Orders.AddAsync(refundOrder);

                if (request.RefundType == RefundType.Objective && originalOrder.Type == "Cash")
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

                try
                {
                    await _realtimeService.NotifyOrderStatusChanged(originalOrder.RestaurantId.ToString(), originalOrder.Id.ToString(), (int)originalOrder.Status);
                    await _realtimeService.NotifyCustomerOrderStatusChanged(originalOrder.Id.ToString(), (int)originalOrder.Status);
                    
                    var restaurant = await _unitOfWork.Restaurants.GetByIdAsync(originalOrder.RestaurantId);
                    if (restaurant != null)
                    {
                        await _realtimeService.NotifyListChanged(restaurant.TenantId.ToString());
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Lỗi SignalR (Refund): {Message}", ex.Message);
                }

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

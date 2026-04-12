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

            string? paymentProofUrl = await UploadProofImageAsync(request.ImageFile, order.OrderCode, "proof");

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
                    ShiftId = activeShift?.Id,
                    TransactionType = TransactionType.Payment
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
            var originalOrder = await ValidateRefundOrderOrThrowAsync(request);

            await using var tx = await _unitOfWork.BeginTransactionAsync();
            try
            {
                if (request.IsFullRefund)
                {
                    originalOrder.Status = OrderStatus.Cancelled;
                    _unitOfWork.Orders.Update(originalOrder);
                }

                var (refundAmount, refundDetails) = PrepareRefundDetails(originalOrder, request);

                // Check if all items in the original order are now fully refunded
                bool isAllItemsRefunded = originalOrder.OrderDetails.All(od => od.RefundedQuantity >= od.Quantity);
                if (!request.IsFullRefund && isAllItemsRefunded && originalOrder.Status != OrderStatus.Cancelled)
                {
                    originalOrder.Status = OrderStatus.Cancelled;
                    _unitOfWork.Orders.Update(originalOrder);
                }

                string? paymentProofUrl = await UploadProofImageAsync(request.ImageFile, originalOrder.OrderCode, "refund_proof");

                var refundOrder = CreateRefundLogEntity(originalOrder, request, refundAmount, paymentProofUrl);
                await _unitOfWork.Orders.AddAsync(refundOrder);

                foreach (var rd in refundDetails)
                {
                    rd.OrderId = refundOrder.Id;
                }
                if (refundDetails.Any())
                {
                    await _unitOfWork.OrderDetails.AddRangeAsync(refundDetails);
                }

                await LogRefundTransactionIfCashAsync(originalOrder, refundOrder, refundAmount, request.RefundType);

                await _unitOfWork.SaveAsync();
                await tx.CommitAsync();

                // 7. Thông báo Realtime
                await SendRefundNotificationsAsync(originalOrder, refundOrder);

                return true;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Lỗi khi xử lý hoàn tiền cho Order {OrderId}", request.OrderId);
                throw;
            }
        }

        private async Task<Order> ValidateRefundOrderOrThrowAsync(RefundRequest request)
        {
            var originalOrder = await _unitOfWork.Orders.GetByFieldsIncludeAsync(
                o => o.Id == request.OrderId,
                o => o.OrderDetails);

            if (originalOrder == null)
            {
                throw new DomainException(OrderMessage.OrderError.ORDER_NOT_FOUND);
            }

            var activeShift = await _unitOfWork.Shifts.FirstOrDefaultAsync(
                s => s.RestaurantId == originalOrder.RestaurantId && s.Status == ShiftStatus.Open);

            if (activeShift == null)
            {
                throw new DomainException("Nhà hàng chưa có ca làm việc nào được mở. Vui lòng Check-in trước khi thực hiện hoàn tiền.");
            }

            if (originalOrder.Status == OrderStatus.Cancelled)
            {
                throw new DomainException(OrderMessage.OrderError.ORDER_ALREADY_CANCELLED_OR_REFUNDED);
            }

            if (originalOrder.Status == OrderStatus.Unpaid)
            {
                throw new DomainException("Đơn hàng chưa thanh toán, không thể thực hiện hoàn tiền. Vui lòng thực hiện hủy đơn nếu cần.");
            }

            if (request.RefundType == RefundType.Objective && (request.ImageFile == null || request.ImageFile.Length == 0))
            {
                throw new DomainException("Trường hợp khách quan bắt buộc phải có ảnh minh chứng chuyển khoản.");
            }

            if (!request.IsFullRefund && (request.RefundItems == null || !request.RefundItems.Any()))
            {
                throw new DomainException("Với trường hợp hoàn tiền một phần, bạn phải chọn ít nhất một món ăn để hoàn.");
            }

            return originalOrder;
        }

        private (decimal totalAmount, List<OrderDetail> details) PrepareRefundDetails(Order originalOrder, RefundRequest request)
        {
            decimal refundAmount = 0;
            var refundDetails = new List<OrderDetail>();

            // Tính hệ số thanh toán thực tế (Sau Voucher)
            decimal paymentRatio = originalOrder.TotalAmount > 0 
                ? originalOrder.FinalAmount / originalOrder.TotalAmount 
                : 1;

            if (request.IsFullRefund)
            {
                refundAmount = originalOrder.FinalAmount;
                foreach (var detail in originalOrder.OrderDetails)
                {
                    detail.RefundedQuantity = detail.Quantity;
                    _unitOfWork.OrderDetails.Update(detail);
                    refundDetails.Add(new OrderDetail
                    {
                        DishId = detail.DishId,
                        Quantity = detail.Quantity,
                        OriginalPrice = detail.OriginalPrice,
                        DiscountedPrice = detail.DiscountedPrice,
                        SubTotal = detail.SubTotal,
                        PromotionAmount = detail.PromotionAmount
                    });
                }
            }
            else
            {
                foreach (var item in request.RefundItems!)
                {
                    if (item.OrderDetailId <= 0 || item.QuantityToRefund <= 0) continue;

                    var originalDetail = originalOrder.OrderDetails.FirstOrDefault(od => od.Id == item.OrderDetailId);
                    if (originalDetail == null) continue;

                    int availableToRefund = originalDetail.Quantity - originalDetail.RefundedQuantity;
                    if (availableToRefund <= 0) continue;

                    int refundQty = Math.Min(item.QuantityToRefund, availableToRefund);
                    if (refundQty <= 0) continue;

                    decimal ratio = (decimal)refundQty / originalDetail.Quantity;
                    
                    decimal rawItemRefund = (originalDetail.SubTotal * ratio) * paymentRatio;
                    decimal itemRefundAmount = (decimal)PricingUtils.RoundToNearestThousand(rawItemRefund);

                    refundAmount += itemRefundAmount;
                    originalDetail.RefundedQuantity += refundQty;
                    _unitOfWork.OrderDetails.Update(originalDetail);

                    refundDetails.Add(new OrderDetail
                    {
                        DishId = originalDetail.DishId,
                        Quantity = refundQty,
                        OriginalPrice = originalDetail.OriginalPrice,
                        DiscountedPrice = originalDetail.DiscountedPrice,
                        SubTotal = itemRefundAmount,
                        PromotionAmount = originalDetail.PromotionAmount * ratio
                    });
                }

                if (!refundDetails.Any())
                {
                    throw new DomainException("Không tìm thấy món ăn hợp lệ trong đơn hàng gốc. Kiểm tra lại OrderDetailId.");
                }
            }

            return (refundAmount, refundDetails);
        }

        private Order CreateRefundLogEntity(Order originalOrder, RefundRequest request, decimal refundAmount, string? proofUrl)
        {
            return new Order
            {
                Id = Guid.NewGuid(),
                RestaurantId = originalOrder.RestaurantId,
                OrderCode = originalOrder.OrderCode,
                RefundOrderId = originalOrder.Id,
                RefundType = request.RefundType,
                ResponsibleStaffId = request.ResponsibleStaffId,
                typeOrder = TypeOrder.Refund,
                Status = OrderStatus.Cancelled,
                NumberPhone = "",
                FinalAmount = refundAmount,
                TotalAmount = refundAmount,
                PromotionDiscount = 0,
                Note = request.Note,
                QrCodeUrl = "REFUND_LOG",
                Type = originalOrder.Type,
                IsPreOrder = false,
                IsScanned = false,
                PaymentProofUrl = proofUrl
            };
        }

        private async Task LogRefundTransactionIfCashAsync(Order originalOrder, Order refundOrder, decimal refundAmount, RefundType refundType)
        {
            if (refundType != RefundType.Objective) return;

            var originalTransaction = await _unitOfWork.Transactions.FirstOrDefaultAsync(
                t => t.OrderId == originalOrder.Id && t.TransactionType == TransactionType.Payment);

            if (originalTransaction?.PaymentMethod == PaymentMethod.Cash)
            {
                var activeShift = await _unitOfWork.Shifts.FirstOrDefaultAsync(
                    s => s.RestaurantId == originalOrder.RestaurantId && s.Status == ShiftStatus.Open);

                var transaction = new Transaction
                {
                    OrderId = refundOrder.Id,
                    TotalAmount = refundAmount,
                    PaymentMethod = PaymentMethod.Cash,
                    Status = OrderTransactionStatus.Success,
                    ShiftId = activeShift?.Id,
                    TransactionType = TransactionType.Refund
                };
                await _unitOfWork.Transactions.AddAsync(transaction);
            }
        }

        private async Task SendRefundNotificationsAsync(Order originalOrder, Order? refundOrder = null)
        {
            try
            {
                // Thông báo trạng thái đơn hàng gốc
                await _realtimeService.NotifyOrderStatusChanged(originalOrder.RestaurantId.ToString(), originalOrder.Id.ToString(), (int)originalOrder.Status);
                await _realtimeService.NotifyCustomerOrderStatusChanged(originalOrder.Id.ToString(), (int)originalOrder.Status);

                // Nếu có đơn Refund log mới, thông báo luôn để app cập nhật danh sách Refund
                if (refundOrder != null)
                {
                    await _realtimeService.NotifyOrderStatusChanged(refundOrder.RestaurantId.ToString(), refundOrder.Id.ToString(), (int)refundOrder.Status);
                }

                var restaurant = await _unitOfWork.Restaurants.GetByIdAsync(originalOrder.RestaurantId);
                if (restaurant != null)
                {
                    // Gửi ListChanged đến cả TenantId và RestaurantId để đảm bảo Staff app và các bên liên quan đều nhận được
                    await _realtimeService.NotifyListChanged(restaurant.TenantId.ToString());
                    await _realtimeService.NotifyListChanged(originalOrder.RestaurantId.ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi SignalR (Refund): {Message}", ex.Message);
            }
        }

        private async Task<string?> UploadProofImageAsync(Microsoft.AspNetCore.Http.IFormFile? imageFile, int orderCode, string prefix)
        {
            if (imageFile == null || imageFile.Length == 0)
            {
                return null;
            }

            try
            {
                using var ms = new MemoryStream();
                await imageFile.CopyToAsync(ms);
                var fileBytes = ms.ToArray();
                string extension = Path.GetExtension(imageFile.FileName);
                string fileName = $"{prefix}_{orderCode}_{Guid.NewGuid()}{extension}";
                return await _storageService.UploadPaymentProofAsync(fileBytes, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi tải ảnh minh chứng ({Prefix}) cho OrderCode {OrderCode}", prefix, orderCode);
                throw new DomainException($"Lỗi khi tải ảnh minh chứng lên: {ex.Message}");
            }
        }
    }
}

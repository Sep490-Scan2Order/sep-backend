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
using ScanToOrder.Domain.Entities.Shifts;

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

                var activeShift = await _unitOfWork.Shifts.GetActiveCashierShiftAsync(order.RestaurantId)
                    ?? throw new DomainException(ShiftMessage.ShiftError.SHIFT_NOT_OPEN_YET);

                var transaction = await _unitOfWork.Transactions.GetTransactionByOrderIdAsync(order.Id);
                
                if (transaction == null)
                {
                    throw new DomainException(OrderMessage.OrderError.TRANSACTION_NOT_FOUND);
                }

                transaction.Status = OrderTransactionStatus.Success;
                transaction.ShiftId = activeShift.Id;
                transaction.TransactionType = TransactionType.Payment;
                _unitOfWork.Transactions.Update(transaction);

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
            var (originalOrder, activeShift) = await ValidateRefundOrderOrThrowAsync(request);

            string? paymentProofUrl = await UploadProofImageAsync(request.ImageFile, originalOrder.OrderCode, "refund_proof");

            await using var tx = await _unitOfWork.BeginTransactionAsync();
            try
            {
                var (refundAmount, refundDetails) = PrepareRefundDetails(originalOrder, request);

                bool isStaffError = request.RefundType == RefundType.StaffError;

                if (request.IsFullRefund)
                {
                    originalOrder.Status = OrderStatus.Cancelled;
                    if (!isStaffError)
                    {
                        originalOrder.FinalAmount = 0;
                    }
                    _unitOfWork.Orders.Update(originalOrder);
                }
                else if (refundAmount > 0)
                {
                    if (!isStaffError)
                    {
                        var newFinal = originalOrder.FinalAmount - refundAmount;
                        if (newFinal < 0)
                            newFinal = 0;
                        originalOrder.FinalAmount = (decimal)PricingUtils.RoundToNearestThousand(newFinal);
                    }
                    _unitOfWork.Orders.Update(originalOrder);
                }

                bool isAllItemsRefunded = originalOrder.OrderDetails.All(od => od.RefundedQuantity >= od.Quantity);
                if (!request.IsFullRefund && isAllItemsRefunded && originalOrder.Status != OrderStatus.Cancelled)
                {
                    originalOrder.Status = OrderStatus.Cancelled;
                    _unitOfWork.Orders.Update(originalOrder);
                }

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

                await LogRefundTransactionIfCashAsync(originalOrder, refundOrder, refundAmount, request.RefundType, activeShift);

                await _unitOfWork.SaveAsync();
                await tx.CommitAsync();

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

        private async Task<(Order originalOrder, Shift activeShift)> ValidateRefundOrderOrThrowAsync(RefundRequest request)
        {
            var originalOrder = await _unitOfWork.Orders.GetOrderWithDetailsByIdAsync(request.OrderId);

            if (originalOrder == null)
            {
                throw new DomainException(OrderMessage.OrderError.ORDER_NOT_FOUND);
            }

            var activeShift = await _unitOfWork.Shifts.GetActiveCashierShiftAsync(originalOrder.RestaurantId);

            if (activeShift == null)
            {
                throw new DomainException(OrderMessage.OrderError.RESTAURANT_SHIFT_NOT_OPENED);
            }

            if (originalOrder.Status == OrderStatus.Cancelled)
            {
                throw new DomainException(OrderMessage.OrderError.ORDER_ALREADY_CANCELLED_OR_REFUNDED);
            }

            if (originalOrder.Status == OrderStatus.Unpaid)
            {
                throw new DomainException(OrderMessage.OrderError.REFUND_UNPAID_ORDER_NOT_SUPPORTED);
            }

            if (request.RefundType == RefundType.Objective && (request.ImageFile == null || request.ImageFile.Length == 0))
            {
                throw new DomainException(OrderMessage.OrderError.REFUND_OBJECTIVE_PROOF_REQUIRED);
            }

            if (!request.IsFullRefund && (request.RefundItems == null || !request.RefundItems.Any()))
            {
                throw new DomainException(OrderMessage.OrderError.PARTIAL_REFUND_ITEMS_REQUIRED);
            }

            return (originalOrder, activeShift);
        }

        private (decimal totalAmount, List<OrderDetail> details) PrepareRefundDetails(Order originalOrder, RefundRequest request)
        {
            decimal refundAmount = 0;
            var refundDetails = new List<OrderDetail>();

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
                var detailMap = originalOrder.OrderDetails.ToDictionary(od => od.Id);

                foreach (var item in request.RefundItems!)
                {
                    if (item.OrderDetailId <= 0 || item.QuantityToRefund <= 0) continue;

                    if (!detailMap.TryGetValue(item.OrderDetailId, out var originalDetail)) continue;

                    int availableToRefund = originalDetail.Quantity - originalDetail.RefundedQuantity;
                    if (availableToRefund <= 0) continue;

                    int refundQty = Math.Min(item.QuantityToRefund, availableToRefund);
                    if (refundQty <= 0) continue;

                    decimal rawItemRefund = (originalDetail.SubTotal * refundQty / originalDetail.Quantity) * paymentRatio;
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
                        PromotionAmount = originalDetail.PromotionAmount * refundQty / originalDetail.Quantity
                    });
                }

                if (!refundDetails.Any())
                {
                    throw new DomainException(OrderMessage.OrderError.REFUND_ITEMS_NOT_FOUND);
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

        private async Task LogRefundTransactionIfCashAsync(Order originalOrder, Order refundOrder, decimal refundAmount, RefundType refundType, Shift? activeShift)
        {
            if (refundType != RefundType.Objective) return;

            var originalTransaction = await _unitOfWork.Transactions.GetPaymentTransactionByOrderIdAsync(originalOrder.Id);

            if (originalTransaction?.PaymentMethod == PaymentMethod.Cash)
            {
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
                await _realtimeService.NotifyOrderStatusChanged(originalOrder.RestaurantId.ToString(), originalOrder.Id.ToString(), (int)originalOrder.Status);
                await _realtimeService.NotifyCustomerOrderStatusChanged(originalOrder.Id.ToString(), (int)originalOrder.Status);

                if (refundOrder != null)
                {
                    await _realtimeService.NotifyOrderStatusChanged(refundOrder.RestaurantId.ToString(), refundOrder.Id.ToString(), (int)refundOrder.Status);
                }

                var restaurant = originalOrder.Restaurant;
                if (restaurant != null)
                {
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
                throw new DomainException(string.Format(OrderMessage.OrderError.UPLOAD_PROOF_ERROR, ex.Message));
            }
        }
    }
}

using System;
using ScanToOrder.Domain.Entities.SubscriptionPlan;

namespace ScanToOrder.Application.DTOs.Payment
{
    public class PaymentTransactionHistoryDto
    {
        public int Id { get; set; }
        public string TransactionCode { get; set; } = null!;
        public decimal TotalAmount { get; set; }
        public DateTime PaymentDate { get; set; }
        public string Status { get; set; } = null!;
        public string PaymentTransactionType { get; set; } = null!;
        
        // Tùy chỉnh dữ liệu trả về cho trường hợp đăng ký gói - thân thiện dễ đọc
        public List<SubscriptionTransactionItemDto>? SubscriptionDetails { get; set; }
        
        // Dữ liệu cho thanh toán nợ hoa hồng
        public CommissionFeePayload? CommissionDetails { get; set; }
    }

    public class SubscriptionTransactionItemDto
    {
        public int RestaurantId { get; set; }
        public string RestaurantName { get; set; } = null!;
        public string ActionType { get; set; } = null!;
        public int? OldPlanId { get; set; }
        public string? OldPlanName { get; set; }
        public int NewPlanId { get; set; }
        public string NewPlanName { get; set; } = null!;
        public string Cycle { get; set; } = null!;
        public int Quantity { get; set; }
        public decimal AmountAllocated { get; set; }
        public decimal BalanceConverted { get; set; }
        public string DescriptionMessage { get; set; } = null!;
    }
}

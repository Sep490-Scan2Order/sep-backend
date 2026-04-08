using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Domain.Entities.SubscriptionPlan
{
    public class PaymentTransaction : BaseEntity<int>
    {
        public Guid TenantId { get; set; }   
        public DateTime PaymentDate { get; set; }
        public string TransactionCode { get; set; } = null!;
        public decimal TotalAmount { get; set; }    
        
        // 1. Đổi kiểu thành string để EF Core map vào cột jsonb dễ dàng
        [Column(TypeName = "jsonb")]
        public string Payload { get; set; } = null!; 

        public PaymentTransactionStatus Status { get; set; }
        public Tenant Tenants { get; set; } = null!;

        public PaymentTransactionType PaymentTransactionType { get; set; }

        // ====================================================================
        // NHỮNG PROPERTY BÊN DƯỚI DÙNG ĐỂ LẤY/GÁN DATA (KHÔNG LƯU VÀO DB)
        // ====================================================================

        // Lấy ra Payload nếu là loại mua gói
        [NotMapped]
        public List<OrderPayloadItemPlan>? SubscriptionPayload
        {
            get
            {
                if (PaymentTransactionType != PaymentTransactionType.Subscription || string.IsNullOrEmpty(Payload))
                    return null;
                return JsonSerializer.Deserialize<List<OrderPayloadItemPlan>>(Payload);
            }
        }

        // Lấy ra Payload nếu là loại trả nợ hoa hồng
        [NotMapped]
        public CommissionFeePayload? CommissionPayload
        {
            get
            {
                if (PaymentTransactionType != PaymentTransactionType.CommissionFee || string.IsNullOrEmpty(Payload))
                    return null;
                return JsonSerializer.Deserialize<CommissionFeePayload>(Payload);
            }
        }

        // Helper method: Gọi hàm này khi tạo giao dịch Mua gói
        public void SetSubscriptionPayload(List<OrderPayloadItemPlan> payloadItems)
        {
            PaymentTransactionType = PaymentTransactionType.Subscription;
            Payload = JsonSerializer.Serialize(payloadItems);
        }

        // Helper method: Gọi hàm này khi tạo giao dịch Trả nợ
        public void SetCommissionPayload(CommissionFeePayload payloadData)
        {
            PaymentTransactionType = PaymentTransactionType.CommissionFee;
            Payload = JsonSerializer.Serialize(payloadData);
        }
    }
}
using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Domain.Entities.Orders
{
    public class Transaction : BaseEntity<int>
    {
        public Guid OrderId { get; set; }       

        public OrderTransactionStatus Status { get; set; }

        public decimal TotalAmount { get; set; }

        public string? TransactionCode { get; set; } = string.Empty;

        public PaymentMethod PaymentMethod { get; set; }
        public virtual Order Order { get; set; } = null!;
    }
}


using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Domain.Entities.SubscriptionPlan
{
    public class PaymentTransaction : BaseEntity<int>
    {
        public Guid TenantId { get; set; }   
        public DateTime PaymentDate { get; set; }

        public string TransactionCode { get; set; } = null!;

        public int TotalAmount { get; set; }    

        public PaymentTransactionStatus Status { get; set; }

        public Tenant Tenants { get; set; } = null!;
    }
}

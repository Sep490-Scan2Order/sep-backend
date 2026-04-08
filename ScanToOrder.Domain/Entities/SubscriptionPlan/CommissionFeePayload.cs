using System;

namespace ScanToOrder.Domain.Entities.SubscriptionPlan
{
    public class CommissionFeePayload
    {
       
        public DateTimeOffset PeriodStart { get; set; }        
       
        public DateTimeOffset PeriodEnd { get; set; }   
        
        public int TotalOrdersScanned { get; set; }     
        
        public decimal TotalOrderAmount { get; set; }   

        public decimal CommissionRate { get; set; }     
    }
}
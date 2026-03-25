using System;

namespace ScanToOrder.Application.DTOs.Orders
{
    public class ConfirmPickupTimeRequest
    {
        public Guid OrderId { get; set; }
        public DateTime ConfirmedPickupAt { get; set; }
    }
}

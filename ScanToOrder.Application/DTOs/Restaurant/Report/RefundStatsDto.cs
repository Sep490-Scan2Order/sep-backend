namespace ScanToOrder.Application.DTOs.Restaurant.Report
{
    public class RefundStatsDto : OrderTypeStatsDto
    {
        public OrderTypeStatsDto Objective { get; set; } = new OrderTypeStatsDto();
        public OrderTypeStatsDto StaffError { get; set; } = new OrderTypeStatsDto();
        public OrderTypeStatsDto SystemError { get; set; } = new OrderTypeStatsDto();
    }
}

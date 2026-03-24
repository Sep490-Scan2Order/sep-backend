namespace ScanToOrder.Application.DTOs.Restaurant.Report
{
    public class RevenueSummaryDto
    {
        public PeriodDto Period { get; set; } = new PeriodDto();
        public SummaryMetricsDto Summary { get; set; } = new SummaryMetricsDto();
        public PaymentMethodsDto PaymentMethods { get; set; } = new PaymentMethodsDto();
        public OrderTypesDto OrderTypes { get; set; } = new OrderTypesDto();
        public List<TopSellingDishDto> TopSellingDishes { get; set; } = new List<TopSellingDishDto>();
    }
   

}

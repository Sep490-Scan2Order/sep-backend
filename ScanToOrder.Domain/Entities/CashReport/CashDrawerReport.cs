using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Entities.User;

namespace ScanToOrder.Domain.Entities.CashReport;

public class CashDrawerReport : BaseEntity<int>
{
    public Guid StaffId { get; set; }
    public int RestaurantId { get; set; }
    public DateOnly ReportDate { get; set; }
    public decimal TotalCashOrder { get; set; }
    public decimal ActualCashAmount { get; set; }
    public decimal Difference { get; set; }

    public virtual Staff Staff { get; set; } = null!;
    public virtual Restaurant.Restaurant Restaurant { get; set; } = null!;
}

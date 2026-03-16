using ScanToOrder.Domain.Entities.Base;

namespace ScanToOrder.Domain.Entities.Shifts;

public class ShiftReport : BaseEntity<int>
{
    public int ShiftId { get; set; }
    public DateTime ReportDate { get; set; }
    public decimal TotalCashOrder { get; set; }  
    public decimal TotalTransferOrder { get; set; }
    public decimal ExpectedCashAmount { get; set; }
    public decimal ActualCashAmount { get; set; }
    public decimal Difference { get; set; }
    public decimal HandoverCashAmount { get; set; }
    public string Note { get; set; } = string.Empty;
    public virtual Shift Shift { get; set; } = null!;
}

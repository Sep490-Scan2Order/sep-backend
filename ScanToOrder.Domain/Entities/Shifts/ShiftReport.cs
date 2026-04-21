using ScanToOrder.Domain.Entities.Base;

namespace ScanToOrder.Domain.Entities.Shifts;

public class ShiftReport : BaseEntity<int>
{
    public int ShiftId { get; set; }
    public DateTime ReportDate { get; set; }

    public decimal TotalCashOrder { get; set; }   
    public decimal TotalTransferOrder { get; set; }   
    public decimal TotalRefundAmount { get; set; }   

    public decimal ActualCashAmount { get; set; }   
    public decimal Difference { get; set; }
    public bool IsTransferred { get; set; } = false;
    public string Note { get; set; } = string.Empty;
    public virtual Shift Shift { get; set; } = null!;
}

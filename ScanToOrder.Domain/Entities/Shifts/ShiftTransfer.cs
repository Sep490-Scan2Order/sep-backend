using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Enums;

namespace ScanToOrder.Domain.Entities.Shifts
{
    public class ShiftTransfer : BaseEntity<int>
    {
        public int ShiftId { get; set; }
        public decimal Amount { get; set; }
        public string? TransactionCode { get; set; }
        public ShiftTransferStatus Status { get; set; }
        public string? Note { get; set; }

        public virtual Shift Shift { get; set; } = null!;
    }
}

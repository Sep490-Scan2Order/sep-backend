using ScanToOrder.Domain.Entities.Vouchers;

namespace ScanToOrder.Domain.Entities.Points;

public class PointHistory
{
    public int PointHistoryId { get; set; }
    public int Point { get; set; }
    public string Type { get; set; } = null!; // Earn, Spend...
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
    public Guid? OrderId { get; set; }

    public int? MemberVoucherId { get; set; }
    public int MemberPointId { get; set; }

    public virtual MemberPoint MemberPoint { get; set; } = null!;
    public virtual MemberVoucher? MemberVoucher { get; set; }
}
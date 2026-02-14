using ScanToOrder.Domain.Entities.User;

namespace ScanToOrder.Domain.Entities.Points
{
    public partial class MemberPoint
    {
        public int MemberPointId { get; set; }
        public int CurrentPoint { get; set; }
        public DateTime RedeemAt { get; set; } = DateTime.Now;

        public Guid CustomerId { get; set; }
        public virtual Customer Customer { get; set; } = null!;
        public virtual ICollection<PointHistory> PointHistories { get; set; } = new List<PointHistory>();
    }
}

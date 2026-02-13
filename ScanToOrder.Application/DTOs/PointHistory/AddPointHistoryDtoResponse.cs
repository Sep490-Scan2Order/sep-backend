namespace ScanToOrder.Application.DTOs.PointHistory
{
    public class AddPointHistoryDtoResponse
    {
        public int PointHistoryId { get; set; }
        public int Point { get; set; }
        public string Type { get; set; } = null!; // Earn, Spend...
        public DateTime CreateDate { get; set; } = DateTime.Now;
        public Guid? OrderId { get; set; }

        public int? MemberVoucherId { get; set; }
        public int MemberPointId { get; set; }
    }
}

namespace ScanToOrder.Application.DTOs.PointHistory
{
    public class AddPointHistoryDtoRequest
    {
        public int Point { get; set; }
        public string Type { get; set; } = null!;
        public DateTime CreateDate { get; set; } = DateTime.Now;
        public Guid? OrderId { get; set; }
        public int MemberPointId { get; set; }
    }
}

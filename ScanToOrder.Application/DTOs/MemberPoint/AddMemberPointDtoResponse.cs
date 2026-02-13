namespace ScanToOrder.Application.DTOs.MemberPoint
{
    public class AddMemberPointDtoResponse
    {
        public int MemberPointId { get; set; }
        public int CurrentPoint { get; set; }
        public DateTime RedeemAt { get; set; } = DateTime.Now;
        public Guid CustomerId { get; set; }
    }
}

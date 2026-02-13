namespace ScanToOrder.Application.DTOs.MemberPoint
{
    public class AddMemberPointDtoRequest
    {
        public int CurrentPoint { get; set; }
        public DateTime RedeemAt { get; set; } = DateTime.Now;

        public Guid CustomerId { get; set; }
    }
}

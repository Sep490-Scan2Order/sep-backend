namespace ScanToOrder.Application.DTOs.User;

public class CustomerDto
{
    public DateOnly? Dob { get; set; }
    public string Name { get; set; } = null!;
    public Guid AccountId { get; set; }
    public Guid Id { get; set; }
}
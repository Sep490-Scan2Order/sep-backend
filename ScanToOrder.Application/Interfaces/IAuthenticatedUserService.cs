namespace ScanToOrder.Application.Interfaces;

public interface IAuthenticatedUserService
{
    public Guid? UserId { get; }
    public Guid? ProfileId { get; }
    public string? Email { get; }
    public string? Phone { get; }
    public string? Role { get; }        
}
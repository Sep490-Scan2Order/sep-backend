namespace ScanToOrder.Application.Services;

public interface IAuthenticatedUserService
{
    public Guid? UserId { get; }
    public string? Email { get; }
    public string? Phone { get; }
    public string? Role { get; }        
}
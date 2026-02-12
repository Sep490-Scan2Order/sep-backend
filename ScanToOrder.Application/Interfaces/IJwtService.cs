using ScanToOrder.Domain.Entities.Authentication;

namespace ScanToOrder.Application.Interfaces
{
    public interface IJwtService
    {
        string GenerateAccessToken(AuthenticationUser user);

        string GenerateRefreshToken(AuthenticationUser user);

        string? ValidateRefreshToken(string refreshToken);
    }
}

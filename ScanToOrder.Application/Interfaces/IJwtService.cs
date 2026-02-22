using ScanToOrder.Domain.Entities.Authentication;

namespace ScanToOrder.Application.Interfaces
{
    public interface IJwtService
    {
        string GenerateAccessToken(AuthenticationUser use, Guid? profileId = null);

        string GenerateRefreshToken(AuthenticationUser user, Guid? profileId = null);

        string? ValidateRefreshToken(string refreshToken);
    }
}

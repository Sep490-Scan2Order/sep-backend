using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ScanToOrder.Application.Interfaces;

namespace ScanToOrder.Infrastructure.Services;

public class AuthenticatedUserService : IAuthenticatedUserService
{
    public Guid? UserId { get; } 
    public Guid? ProfileId { get; }
    public string? Email { get; }
    public string? Phone { get; }
    public string? Role { get; }
    
    public AuthenticatedUserService(IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;
            
        if (user?.Identity?.IsAuthenticated != true) return;

        var strId = user.FindFirstValue(JwtRegisteredClaimNames.Sub) 
                    ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
                     
        if (Guid.TryParse(strId, out var userId))
        {
            UserId = userId;
        }

        var strProfileId = user.FindFirstValue("ProfileId");
        if (Guid.TryParse(strProfileId, out var pId))
        {
            ProfileId = pId;
        }

        Email = user.FindFirstValue(ClaimTypes.Email);
        Phone = user.FindFirstValue(ClaimTypes.MobilePhone);
        Role = user.FindFirstValue(ClaimTypes.Role);
    }
}
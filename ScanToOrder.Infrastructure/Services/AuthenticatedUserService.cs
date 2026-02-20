using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ScanToOrder.Application.Services;
using Microsoft.AspNetCore.Http;

namespace ScanToOrder.Infrastructure.Services;

public class AuthenticatedUserService : IAuthenticatedUserService
{
    public Guid? UserId { get; }
    public string? Email { get; }
    public string? Phone { get; }
    public string? Role { get; }
    
    public AuthenticatedUserService(IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;
            
        if (user?.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedAccessException("Người dùng chưa được xác thực hoặc không có token.");
        }

        var strId = user.FindFirstValue(JwtRegisteredClaimNames.Sub) 
                    ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
                     
        if (string.IsNullOrEmpty(strId))
        {
            throw new UnauthorizedAccessException("Token không chứa thông tin ID người dùng hợp lệ.");
        }

        if (!Guid.TryParse(strId, out var id))
        {
            throw new UnauthorizedAccessException("Định dạng ID người dùng trong token không đúng chuẩn Guid.");
        }

        UserId = id;
        Email = user.FindFirstValue(ClaimTypes.Email);
        Phone = user.FindFirstValue(ClaimTypes.MobilePhone);
        Role = user.FindFirstValue(ClaimTypes.Role);
    }
}
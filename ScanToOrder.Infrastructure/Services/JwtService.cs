using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Infrastructure.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ScanToOrder.Infrastructure.Services
{
    public class JwtService : IJwtService
    {
        private readonly JwtSettings _settings;

        public JwtService(IOptions<JwtSettings> jwtSetting)
        {
            _settings = jwtSetting.Value;
        }

        public string GenerateAccessToken(AuthenticationUser user)
        {
            var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), 
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.MobilePhone, user.Phone ?? string.Empty)
        };

            claims.Add(new Claim(ClaimTypes.Role, user.Role.ToString()));

            return CreateToken(claims, _settings.AccessSecretKey, _settings.AccessExpiration);
        }

        public string GenerateRefreshToken(AuthenticationUser user)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString())
            };

            return CreateToken(claims, _settings.RefreshSecretKey, _settings.RefreshExpiration);
        }

        public string? ValidateRefreshToken(string refreshToken)
        {
            return ValidateToken(refreshToken, _settings.RefreshSecretKey);
        }

        private string CreateToken(List<Claim> claims, string secretKey, int expirationMinutes)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _settings.Issuer,
                audience: _settings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expirationMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string? ValidateToken(string token, string secretKey)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(secretKey);

            try
            {
                var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _settings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero 
                }, out _);

                return principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            }
            catch
            {
                return null;
            }
        }
    }
}

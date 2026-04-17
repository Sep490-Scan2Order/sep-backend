using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;
using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Infrastructure.Configuration;
using ScanToOrder.Infrastructure.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Xunit;

namespace ScanToOrder.Infrastructure.Tests.Services
{
    public class JwtServiceTests
    {
        private readonly Mock<IOptions<JwtSettings>> _jwtSettingsMock;
        private readonly JwtSettings _settings;
        private readonly JwtService _jwtService;

        public JwtServiceTests()
        {
            _settings = new JwtSettings
            {
                AccessSecretKey = "7a0ca08f20aff3cca064ee76bac26cce5721e512fb95315b004ff0c353fc9c46",
                RefreshSecretKey = "2e4c716bce17677a4a3480cf71195d01f366cc5abb806f4461c78c1268a69dd8",

                Issuer = "ScanToOrder_Api",
                Audience = "ScanToOrder_Client",

                AccessExpiration = 60,
                RefreshExpiration = 10080 // 1 tuần
            };

            _jwtSettingsMock = new Mock<IOptions<JwtSettings>>();
            _jwtSettingsMock.Setup(x => x.Value).Returns(_settings);

            _jwtService = new JwtService(_jwtSettingsMock.Object);
        }

        [Fact]
        public void GenerateAccessToken_NoProfileId_ShouldNotContainProfileIdClaim()
        {
            // Arrange
            var user = new AuthenticationUser { Id = Guid.NewGuid(), Email = "test@gmail.com", Role = Role.Admin };

            // Act
            var token = _jwtService.GenerateAccessToken(user, null); // profileId = null

            // Assert
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            jwtToken.Claims.Should().NotContain(c => c.Type == "ProfileId");
        }

        [Theory]
        [InlineData("invalid-token-format")] // Sai định dạng
        [InlineData("")]                     // Chuỗi rỗng
        public void ValidateRefreshToken_InvalidFormat_ShouldReturnNull(string token)
        {
            // Act
            var result = _jwtService.ValidateRefreshToken(token);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void ValidateRefreshToken_WrongKey_ShouldReturnNull()
        {
            // Arrange
            var user = new AuthenticationUser { Id = Guid.NewGuid() };

            // Tạo token bằng một Key khác hoàn toàn
            var settingsWithDifferentKey = new JwtSettings
            {
                RefreshSecretKey = "Key_Nay_Khac_Voi_Key_Trong_Service_123456",
                Issuer = _settings.Issuer,
                Audience = _settings.Audience,
                RefreshExpiration = 10
            };
            var mockOptions = new Mock<IOptions<JwtSettings>>();
            mockOptions.Setup(x => x.Value).Returns(settingsWithDifferentKey);
            var serviceDifferent = new JwtService(mockOptions.Object);
            var tokenWithWrongKey = serviceDifferent.GenerateRefreshToken(user);

            // Act
            var result = _jwtService.ValidateRefreshToken(tokenWithWrongKey);

            // Assert
            result.Should().BeNull(); // Trả về null vì ValidateIssuerSigningKey = true
        }

        [Fact]
        public void GenerateAccessToken_ShouldReturnValidToken_WithCorrectClaims()
        {
            // Arrange
            var user = new AuthenticationUser
            {
                Id = Guid.NewGuid(),
                Email = "admin@scan2order.io.vn",
                Phone = "0123456789",
                Role = Role.Admin
            };
            var profileId = Guid.NewGuid();

            var token = _jwtService.GenerateAccessToken(user, profileId);

            token.Should().NotBeNullOrEmpty();

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            jwtToken.Issuer.Should().Be(_settings.Issuer);
            jwtToken.Audiences.Should().Contain(_settings.Audience);

            jwtToken.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value.Should().Be(user.Id.ToString());
            jwtToken.Claims.First(c => c.Type == ClaimTypes.Email).Value.Should().Be(user.Email);
            jwtToken.Claims.First(c => c.Type == ClaimTypes.Role).Value.Should().Be("Admin");
            jwtToken.Claims.First(c => c.Type == "ProfileId").Value.Should().Be(profileId.ToString());
        }

        [Fact]
        public void GenerateRefreshToken_ShouldReturnValidToken()
        {
            var user = new AuthenticationUser { Id = Guid.NewGuid() };

            var token = _jwtService.GenerateRefreshToken(user);

            // Assert
            token.Should().NotBeNullOrEmpty();

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            jwtToken.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value.Should().Be(user.Id.ToString());
        }

        [Fact]
        public void ValidateRefreshToken_ValidToken_ShouldReturnUserId()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var user = new AuthenticationUser { Id = userId };
            var token = _jwtService.GenerateRefreshToken(user);

            // Act
            var result = _jwtService.ValidateRefreshToken(token);

            // Assert
            result.Should().Be(userId.ToString());
        }

        [Fact]
        public void ValidateRefreshToken_InvalidToken_ShouldReturnNull()
        {
            // Arrange
            var invalidToken = "chuoi-token-khong-hop-le";

            // Act
            var result = _jwtService.ValidateRefreshToken(invalidToken);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void ValidateRefreshToken_ExpiredToken_ShouldReturnNull()
        {
    
            var expiredSettings = new JwtSettings
            {
                AccessSecretKey = _settings.AccessSecretKey,
                RefreshSecretKey = _settings.RefreshSecretKey,
                Issuer = _settings.Issuer,
                Audience = _settings.Audience,
                RefreshExpiration = -10 
            };

            var mockOptions = new Mock<IOptions<JwtSettings>>();
            mockOptions.Setup(x => x.Value).Returns(expiredSettings);
            var serviceWithExpiredToken = new JwtService(mockOptions.Object);

            var user = new AuthenticationUser { Id = Guid.NewGuid() };
            var token = serviceWithExpiredToken.GenerateRefreshToken(user);

            // Act
            var result = _jwtService.ValidateRefreshToken(token);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GenerateAccessToken_WhenProfileIdIsNull_ShouldNotIncludeProfileIdClaim()
        {
            // Arrange
            var user = new AuthenticationUser { Id = Guid.NewGuid(), Email = "test@gmail.com", Role = Role.Admin };

            // Act
            var token = _jwtService.GenerateAccessToken(user, null);

            // Assert
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            // Nhánh này đảm bảo dòng claims.Add cho ProfileId không được thực hiện
            jwtToken.Claims.Should().NotContain(c => c.Type == "ProfileId");
        }

        [Theory]
        [InlineData(null)]              // Case token null
        [InlineData("")]                // Case token rỗng
        [InlineData("not-a-jwt-token")] // Case sai định dạng JWT hoàn toàn
        public void ValidateToken_WhenTokenIsInvalid_ShouldReturnNull(string invalidToken)
        {
            // Act
            var result = _jwtService.ValidateRefreshToken(invalidToken);

            // Assert
            // Nhánh này sẽ phủ được khối 'catch' trong ValidateToken
            result.Should().BeNull();
        }

        [Fact]
        public void ValidateToken_WhenSignatureIsInvalid_ShouldReturnNull()
        {
            // Arrange
            var user = new AuthenticationUser { Id = Guid.NewGuid() };
            var token = _jwtService.GenerateRefreshToken(user);

            // Làm giả token bằng cách thay đổi một ký tự trong chuỗi chữ ký (phần cuối của JWT)
            var tamperedToken = token + "modified";

            // Act
            var result = _jwtService.ValidateRefreshToken(tamperedToken);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void GenerateAccessToken_WhenEmailAndPhoneAreNull_ShouldUseEmptyString()
        {
            // Arrange
            var user = new AuthenticationUser
            {
                Id = Guid.NewGuid(),
                Email = null,
                Phone = null,
                Role = Role.Admin
            };

            // Act
            var token = _jwtService.GenerateAccessToken(user, null);

            // Assert
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            jwtToken.Claims.First(c => c.Type == ClaimTypes.Email).Value.Should().Be(string.Empty);
            jwtToken.Claims.First(c => c.Type == ClaimTypes.MobilePhone).Value.Should().Be(string.Empty);
        }

        [Fact]
        public void ValidateToken_WhenClaimsAreMissing_ShouldReturnNull()
        {
            // Arrange: Tự tạo 1 token thủ công có chữ ký đúng nhưng rỗng Claims
            var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_settings.RefreshSecretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtSecurityToken = new JwtSecurityToken(
                issuer: _settings.Issuer,
                audience: _settings.Audience,
                claims: new List<Claim>(), // Rỗng claims
                expires: DateTime.UtcNow.AddMinutes(10),
                signingCredentials: creds
            );
            var tokenWithoutClaims = tokenHandler.WriteToken(jwtSecurityToken);

            // Act
            var result = _jwtService.ValidateRefreshToken(tokenWithoutClaims);

            // Assert
            // Nhánh này phủ dòng: principal.FindFirst(...)?.Value (trường hợp FindFirst trả về null)
            result.Should().BeNull();
        }

        [Theory]
        [InlineData("Wrong_Issuer", "ScanToOrder_Client")]
        [InlineData("ScanToOrder_Api", "Wrong_Audience")]
        public void ValidateToken_WhenIssuerOrAudienceMismatch_ShouldReturnNull(string issuer, string audience)
        {
            // Arrange: Tạo token với Issuer hoặc Audience sai
            var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_settings.RefreshSecretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var jwtSecurityToken = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: new List<Claim> { new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()) },
                expires: DateTime.UtcNow.AddMinutes(10),
                signingCredentials: creds
            );
            var invalidToken = new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken);

            // Act
            var result = _jwtService.ValidateRefreshToken(invalidToken);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void ValidateToken_WhenNameIdentifierExists_ShouldReturnIdentifierValue()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.RefreshSecretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Issuer = _settings.Issuer,
                Audience = _settings.Audience,
                Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }),
                Expires = DateTime.UtcNow.AddMinutes(10),
                SigningCredentials = creds
            };
            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateEncodedJwt(tokenDescriptor);

            // Act
            var result = _jwtService.ValidateRefreshToken(token);

            // Assert
            result.Should().Be(userId);
        }

        [Fact]
        public void ValidateToken_WhenBothClaimsMissing_ShouldReturnNull()
        {
            // Arrange
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.RefreshSecretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Token hợp lệ nhưng chỉ chứa Role, không chứa Sub hay NameIdentifier
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Issuer = _settings.Issuer,
                Audience = _settings.Audience,
                Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, "Admin") }),
                Expires = DateTime.UtcNow.AddMinutes(10),
                SigningCredentials = creds
            };
            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateEncodedJwt(tokenDescriptor);

            // Act
            var result = _jwtService.ValidateRefreshToken(token);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public void ValidateToken_WhenOnlySubClaimExists_ShouldReturnSubValue()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.RefreshSecretKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            // Tắt map mặc định (sub -> NameIdentifier) để ép chạy nhánh phải của toán tử ??
            var originalMapInboundClaims = JwtSecurityTokenHandler.DefaultMapInboundClaims;
            JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

            try
            {
                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Issuer = _settings.Issuer,
                    Audience = _settings.Audience,
                    Subject = new ClaimsIdentity(new[] { new Claim("sub", userId) }),
                    Expires = DateTime.UtcNow.AddMinutes(10),
                    SigningCredentials = creds
                };

                var tokenHandler = new JwtSecurityTokenHandler();
                var jwtToken = tokenHandler.CreateJwtSecurityToken(tokenDescriptor);
                var token = tokenHandler.WriteToken(jwtToken);

                // Act
                var result = _jwtService.ValidateRefreshToken(token);

                // Assert
                result.Should().Be(userId);
            }
            finally
            {
                JwtSecurityTokenHandler.DefaultMapInboundClaims = originalMapInboundClaims;
            }
        }
    }
}
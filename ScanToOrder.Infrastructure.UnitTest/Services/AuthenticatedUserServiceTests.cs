using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using Moq;
using FluentAssertions;
using ScanToOrder.Infrastructure.Services;

namespace ScanToOrder.Infrastructure.UnitTest.Services
{
    public class AuthenticatedUserServiceTests
    {
        private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;

        public AuthenticatedUserServiceTests()
        {
            _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        }

        [Fact]
        public void Constructor_WhenUserIsAuthenticated_ShouldPopulateProperties()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var profileId = Guid.NewGuid();
            var email = "administrator@scan2order.id.vn";
            var phone = "sep490";
            var role = "Admin";

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim("ProfileId", profileId.ToString()),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.MobilePhone, phone),
                new Claim(ClaimTypes.Role, role)
            };

            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);

            var httpContext = new DefaultHttpContext { User = claimsPrincipal };
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

            // Act
            var service = new AuthenticatedUserService(_mockHttpContextAccessor.Object);

            // Assert
            service.UserId.Should().Be(userId);
            service.ProfileId.Should().Be(profileId);
            service.Email.Should().Be(email);
            service.Phone.Should().Be(phone);
            service.Role.Should().Be(role);
        }

        [Fact]
        public void Constructor_WhenUserIdIsInNameIdentifierClaim_ShouldPopulateUserId()
        {
            // Arrange: Trường hợp Sub null nhưng NameIdentifier có giá trị
            var userId = Guid.NewGuid();
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            };

            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

            // Act
            var service = new AuthenticatedUserService(_mockHttpContextAccessor.Object);

            // Assert
            service.UserId.Should().Be(userId);
        }

        [Fact]
        public void Constructor_WhenUserNotAuthenticated_ShouldKeepPropertiesNull()
        {
            // Arrange: Identity không có IsAuthenticated = true
            var identity = new ClaimsIdentity();
            var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

            // Act
            var service = new AuthenticatedUserService(_mockHttpContextAccessor.Object);

            // Assert
            service.UserId.Should().BeNull();
            service.ProfileId.Should().BeNull();
            service.Email.Should().BeNull();
            service.Role.Should().BeNull();
        }

        [Fact]
        public void Constructor_WhenHttpContextIsNull_ShouldKeepPropertiesNull()
        {
            // Arrange
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns((HttpContext)null);

            // Act
            var service = new AuthenticatedUserService(_mockHttpContextAccessor.Object);

            // Assert
            service.UserId.Should().BeNull();
            service.ProfileId.Should().BeNull();
        }

        [Fact]
        public void Constructor_WhenClaimsAreInvalidGuids_ShouldKeepGuidPropertiesNull()
        {
            // Arrange
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, "not-a-guid"),
                new Claim("ProfileId", "invalid-guid")
            };

            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

            // Act
            var service = new AuthenticatedUserService(_mockHttpContextAccessor.Object);

            // Assert
            service.UserId.Should().BeNull();
            service.ProfileId.Should().BeNull();
        }

        [Fact]
        public void Constructor_WhenOptionalClaimsAreMissing_ShouldStillWork()
        {
            // Arrange: Chỉ cung cấp UserId, thiếu hẳn Email, Phone, Role
            var userId = Guid.NewGuid();
            var claims = new List<Claim> { new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()) };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
            _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

            // Act
            var service = new AuthenticatedUserService(_mockHttpContextAccessor.Object);

            // Assert
            service.UserId.Should().Be(userId);
            service.Email.Should().BeNull(); // Kiểm tra xem có bị crash khi thiếu claim không
            service.ProfileId.Should().BeNull();
        }
    }
}
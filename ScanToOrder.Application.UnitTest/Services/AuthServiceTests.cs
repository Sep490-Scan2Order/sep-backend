using AutoMapper;
using FluentAssertions;
using Moq;
using ScanToOrder.Application.DTOs.Auth;
using ScanToOrder.Application.DTOs.User;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using StackExchange.Redis;
using System.Linq.Expressions;
using UserRole = ScanToOrder.Domain.Enums.Role;

namespace ScanToOrder.Application.UnitTest.Services;

public class AuthServiceTests
{
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<IJwtService> _mockJwtService;
    private readonly Mock<ISmsSender> _mockSmsSender;
    private readonly Mock<IOtpRedisService> _mockOtpRedisService;
    private readonly Mock<IConnectionMultiplexer> _mockConnectionMultiplexer;
    private readonly Mock<IDatabase> _mockRedisDb;
    private readonly Mock<IMapper> _mockMapper;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockJwtService = new Mock<IJwtService>();
        _mockSmsSender = new Mock<ISmsSender>();
        _mockOtpRedisService = new Mock<IOtpRedisService>();
        _mockConnectionMultiplexer = new Mock<IConnectionMultiplexer>();
        _mockRedisDb = new Mock<IDatabase>();
        _mockMapper = new Mock<IMapper>();

        _mockConnectionMultiplexer
            .Setup(c => c.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_mockRedisDb.Object);

        _authService = new AuthService(
            _mockUnitOfWork.Object,
            _mockJwtService.Object,
            _mockSmsSender.Object,
            _mockOtpRedisService.Object,
            _mockConnectionMultiplexer.Object,
            _mockMapper.Object
        );
    }

    #region 1. TenantLoginAsync
    [Fact]
    public async Task TenantLoginAsync_WhenUserNotFound_ThrowsDomainException()
    {
        _mockUnitOfWork.Setup(u => u.AuthenticationUsers.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((AuthenticationUser)null);

        var action = async () => await _authService.TenantLoginAsync(new TenantLoginRequest { Email = "wrong@test.com" });

        await action.Should().ThrowAsync<DomainException>().WithMessage(AuthMessage.AuthError.ACCOUNT_NOT_FOUND);
    }

    [Fact]
    public async Task TenantLoginAsync_WhenPasswordEmpty_ThrowsDomainException()
    {
        var user = new AuthenticationUser { Role = UserRole.Tenant, Password = "", IsActive = true };
        _mockUnitOfWork.Setup(u => u.AuthenticationUsers.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);

        var action = async () => await _authService.TenantLoginAsync(new TenantLoginRequest { Email = "test@test.com" });

        await action.Should().ThrowAsync<DomainException>().WithMessage(AuthMessage.AuthError.ACCOUNT_NO_PASSWORD);
    }

    [Fact]
    public async Task TenantLoginAsync_WhenAccountLocked_ThrowsDomainException()
    {
        var user = new AuthenticationUser { Role = UserRole.Tenant, Password = "hash", IsActive = false };
        _mockUnitOfWork.Setup(u => u.AuthenticationUsers.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);

        var action = async () => await _authService.TenantLoginAsync(new TenantLoginRequest { Email = "test@test.com" });

        await action.Should().ThrowAsync<DomainException>().WithMessage(AuthMessage.AuthError.ACCOUNT_LOCKED);
    }

    [Fact]
    public async Task TenantLoginAsync_WhenWrongPassword_ThrowsDomainException()
    {
        var user = new AuthenticationUser { Role = UserRole.Tenant, Password = BCrypt.Net.BCrypt.HashPassword("correct_password"), IsActive = true };
        _mockUnitOfWork.Setup(u => u.AuthenticationUsers.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);

        var action = async () => await _authService.TenantLoginAsync(new TenantLoginRequest { Email = "test@test.com", Password = "wrong_password" });

        await action.Should().ThrowAsync<DomainException>().WithMessage(AuthMessage.AuthError.ACCOUNT_WRONG_PASSWORD);
    }

    [Fact]
    public async Task TenantLoginAsync_WhenValidCredentials_ReturnsAuthResponse()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var user = new AuthenticationUser
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Tenant,
            Password = BCrypt.Net.BCrypt.HashPassword("password123"),
            IsActive = true,
            Tenant = new Tenant { Id = tenantId} 
        };

        _mockUnitOfWork.Setup(u => u.AuthenticationUsers.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);
        _mockUnitOfWork.Setup(u => u.Tenants.GetTenantWithSubscriptionByAccountIdAsync(user.Id))
            .ReturnsAsync(new Tenant { Id = tenantId});
        _mockMapper.Setup(m => m.Map<TenantDto>(It.IsAny<Tenant>())).Returns(new TenantDto());

        // Act
        var result = await _authService.TenantLoginAsync(new TenantLoginRequest { Email = "test@test.com", Password = "password123" });

        // Assert
        result.Should().NotBeNull();
        _mockJwtService.Verify(j => j.GenerateAccessToken(user, tenantId), Times.Once);
    }
    
    [Fact]
    public async Task TenantLoginAsync_WhenTenantIsNull_ReturnsAuthResponse()
    {
        // Arrange
        var user = new AuthenticationUser
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Tenant,
            Password = BCrypt.Net.BCrypt.HashPassword("password123"),
            IsActive = true,
            Tenant = null 
        };

        _mockUnitOfWork.Setup(u => u.AuthenticationUsers.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);
        _mockUnitOfWork.Setup(u => u.Tenants.GetTenantWithSubscriptionByAccountIdAsync(user.Id))
            .ReturnsAsync(new Tenant { Id = Guid.NewGuid() }); 
        _mockMapper.Setup(m => m.Map<TenantDto>(It.IsAny<Tenant>())).Returns(new TenantDto());

        // Act
        var result = await _authService.TenantLoginAsync(new TenantLoginRequest { Email = "test@test.com", Password = "password123" });

        // Assert
        result.Should().NotBeNull();
    }
    #endregion

    #region 2. StaffLoginAsync
    [Fact]
    public async Task StaffLoginAsync_WhenRoleIsInvalid_ThrowsDomainException()
    {
        var user = new AuthenticationUser { Role = UserRole.Admin }; // Admin không được login qua StaffLogin
        _mockUnitOfWork.Setup(u => u.AuthenticationUsers.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);

        var action = async () => await _authService.StaffLoginAsync(new StaffLoginRequest { Email = "admin@test.com" });

        await action.Should().ThrowAsync<DomainException>().WithMessage(AuthMessage.AuthError.ACCOUNT_NOT_FOUND);
    }

    [Fact]
    public async Task StaffLoginAsync_WhenPasswordEmpty_ThrowsDomainException()
    {
        var user = new AuthenticationUser { Role = UserRole.Staff, Password = "", IsActive = true };
        _mockUnitOfWork.Setup(u => u.AuthenticationUsers.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);

        var action = async () => await _authService.StaffLoginAsync(new StaffLoginRequest { Email = "staff@test.com" });

        await action.Should().ThrowAsync<DomainException>().WithMessage(AuthMessage.AuthError.ACCOUNT_NO_PASSWORD);
    }

    [Fact]
    public async Task StaffLoginAsync_WhenAccountLocked_ThrowsDomainException()
    {
        var user = new AuthenticationUser { Role = UserRole.Staff, Password = "hash", IsActive = false };
        _mockUnitOfWork.Setup(u => u.AuthenticationUsers.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);

        var action = async () => await _authService.StaffLoginAsync(new StaffLoginRequest { Email = "staff@test.com" });

        await action.Should().ThrowAsync<DomainException>().WithMessage(AuthMessage.AuthError.ACCOUNT_LOCKED);
    }

    [Fact]
    public async Task StaffLoginAsync_WhenWrongPassword_ThrowsDomainException()
    {
        var user = new AuthenticationUser { Role = UserRole.Staff, Password = BCrypt.Net.BCrypt.HashPassword("correct_password"), IsActive = true };
        _mockUnitOfWork.Setup(u => u.AuthenticationUsers.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);

        var action = async () => await _authService.StaffLoginAsync(new StaffLoginRequest { Email = "staff@test.com", Password = "wrong_password" });

        await action.Should().ThrowAsync<DomainException>().WithMessage(AuthMessage.AuthError.ACCOUNT_WRONG_PASSWORD_PHONE);
    }

    [Fact]
    public async Task StaffLoginAsync_WhenFirstTimeLogin_SetsVerifiedTrue()
    {
        // Arrange
        var staffId = Guid.NewGuid();
        var user = new AuthenticationUser
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Staff,
            Password = BCrypt.Net.BCrypt.HashPassword("123"),
            IsActive = true,
            Verified = false
        };
        var staff = new Staff { Id = staffId, RestaurantId = 1 };

        _mockUnitOfWork.Setup(u => u.AuthenticationUsers.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);
        _mockUnitOfWork.Setup(u => u.Staffs.GetStaffAccountIdAsync(user.Id)).ReturnsAsync(staff);
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(1))
            .ReturnsAsync(new Restaurant { RestaurantName = "Res Test", Slug = "res-test" }); // Sửa lỗi Required Slug
        _mockMapper.Setup(m => m.Map<StaffDto>(It.IsAny<Staff>())).Returns(new StaffDto());

        // Act
        await _authService.StaffLoginAsync(new StaffLoginRequest { Email = "staff@test.com", Password = "123" });

        // Assert
        user.Verified.Should().BeTrue();
        _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
    }
    
    [Fact]
    public async Task StaffLoginAsync_WhenUserIsNull_ThrowsDomainException()
    {
        // Arrange
        _mockUnitOfWork.Setup(u => u.AuthenticationUsers.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((AuthenticationUser)null);

        // Act
        var action = async () => await _authService.StaffLoginAsync(new StaffLoginRequest { Email = "notfound@test.com" });

        // Assert
        await action.Should().ThrowAsync<DomainException>()
            .WithMessage(AuthMessage.AuthError.ACCOUNT_NOT_FOUND);
    }
    
    [Fact]
    public async Task StaffLoginAsync_WhenRoleIsStaffAndValidCredentials_ReturnsAuthResponse()
    {
        // Arrange
        var staffId = Guid.NewGuid();
        var user = new AuthenticationUser
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Staff,
            Password = BCrypt.Net.BCrypt.HashPassword("password123"),
            IsActive = true,
            Verified = true,
            Staff = new Staff { Id = staffId, RestaurantId = 1 } 
        };
        var staff = new Staff { Id = staffId, RestaurantId = 1 };

        _mockUnitOfWork.Setup(u => u.AuthenticationUsers.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);
        _mockUnitOfWork.Setup(u => u.Staffs.GetStaffAccountIdAsync(user.Id)).ReturnsAsync(staff);
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(1))
            .ReturnsAsync(new Restaurant { RestaurantName = "Res Test", Slug = "res-test" });
        _mockMapper.Setup(m => m.Map<StaffDto>(It.IsAny<Staff>())).Returns(new StaffDto());

        // Act
        var result = await _authService.StaffLoginAsync(new StaffLoginRequest { Email = "staff@test.com", Password = "password123" });

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task StaffLoginAsync_WhenRoleIsCashierAndValidCredentials_ReturnsAuthResponse()
    {
        // Arrange
        var staffId = Guid.NewGuid();
        var user = new AuthenticationUser
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Cashier, 
            Password = BCrypt.Net.BCrypt.HashPassword("password123"),
            IsActive = true,
            Verified = true,
            Staff = new Staff { Id = staffId, RestaurantId = 1 }
        };
        var staff = new Staff { Id = staffId, RestaurantId = 1 };

        _mockUnitOfWork.Setup(u => u.AuthenticationUsers.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);
        _mockUnitOfWork.Setup(u => u.Staffs.GetStaffAccountIdAsync(user.Id)).ReturnsAsync(staff);
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(1))
            .ReturnsAsync(new Restaurant { RestaurantName = "Res Test", Slug = "res-test" });
        _mockMapper.Setup(m => m.Map<StaffDto>(It.IsAny<Staff>())).Returns(new StaffDto());

        // Act
        var result = await _authService.StaffLoginAsync(new StaffLoginRequest { Email = "cashier@test.com", Password = "password123" });

        // Assert
        result.Should().NotBeNull();
    }
    
    [Fact]
    public async Task StaffLoginAsync_WhenRoleIsCashierAndStaffIsNull_ReturnsAuthResponse()
    {
        // Arrange
        var user = new AuthenticationUser
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Cashier,
            Password = BCrypt.Net.BCrypt.HashPassword("password123"),
            IsActive = true,
            Verified = true,
            Staff = null 
        };

        _mockUnitOfWork.Setup(u => u.AuthenticationUsers.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);
        _mockUnitOfWork.Setup(u => u.Staffs.GetStaffAccountIdAsync(user.Id)).ReturnsAsync(new Staff { RestaurantId = 1 });
        _mockUnitOfWork.Setup(u => u.Restaurants.GetByIdAsync(1))
            .ReturnsAsync(new Restaurant { RestaurantName = "Res Test", Slug = "res-test" });
        _mockMapper.Setup(m => m.Map<StaffDto>(It.IsAny<Staff>())).Returns(new StaffDto());

        // Act
        var result = await _authService.StaffLoginAsync(new StaffLoginRequest { Email = "cashier_null@test.com", Password = "password123" });

        // Assert
        result.Should().NotBeNull();
    }
    #endregion

    #region 3. AdministratorLoginAsync
    [Fact]
    public async Task AdministratorLoginAsync_WhenUserNotFound_ThrowsDomainException()
    {
        _mockUnitOfWork.Setup(u => u.AuthenticationUsers.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync((AuthenticationUser)null);

        var action = async () => await _authService.AdministratorLoginAsync(new AdminLoginRequest { Email = "admin@test.com" });

        await action.Should().ThrowAsync<DomainException>().WithMessage(AuthMessage.AuthError.ACCOUNT_NOT_FOUND);
    }

    [Fact]
    public async Task AdministratorLoginAsync_WhenPasswordEmpty_ThrowsDomainException()
    {
        var user = new AuthenticationUser { Role = UserRole.Admin, Password = "", IsActive = true };
        _mockUnitOfWork.Setup(u => u.AuthenticationUsers.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);

        var action = async () => await _authService.AdministratorLoginAsync(new AdminLoginRequest { Email = "admin@test.com" });

        await action.Should().ThrowAsync<DomainException>().WithMessage(AuthMessage.AuthError.ACCOUNT_NO_PASSWORD);
    }

    [Fact]
    public async Task AdministratorLoginAsync_WhenAccountLocked_ThrowsDomainException()
    {
        var user = new AuthenticationUser { Role = UserRole.Admin, Password = "hash", IsActive = false };
        _mockUnitOfWork.Setup(u => u.AuthenticationUsers.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);

        var action = async () => await _authService.AdministratorLoginAsync(new AdminLoginRequest { Email = "admin@test.com" });

        await action.Should().ThrowAsync<DomainException>().WithMessage(AuthMessage.AuthError.ACCOUNT_LOCKED);
    }

    [Fact]
    public async Task AdministratorLoginAsync_WhenWrongPassword_ThrowsDomainException()
    {
        var user = new AuthenticationUser { Role = UserRole.Admin, Password = "AdminPassword@123", IsActive = true };
        _mockUnitOfWork.Setup(u => u.AuthenticationUsers.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);

        var action = async () => await _authService.AdministratorLoginAsync(new AdminLoginRequest { Email = "admin@test.com", Password = "WrongPassword@123" });

        await action.Should().ThrowAsync<DomainException>().WithMessage(AuthMessage.AuthError.ACCOUNT_WRONG_PASSWORD);
    }

    [Fact]
    public async Task AdministratorLoginAsync_WhenPasswordCorrect_ReturnsResponse()
    {
        // Admin dùng so sánh bằng (==) trong code của bạn
        var user = new AuthenticationUser { Role = UserRole.Admin, Password = "AdminPassword@123", IsActive = true };
        _mockUnitOfWork.Setup(u => u.AuthenticationUsers.GetByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);

        var result = await _authService.AdministratorLoginAsync(new AdminLoginRequest { Email = "admin@test.com", Password = "AdminPassword@123" });

        result.Should().NotBeNull();
    }
    #endregion

    #region 4. OTP & Forgot Password
    [Fact]
    public async Task VerifyForgotPasswordOtpAsync_WhenValid_ReturnsToken()
    {
        // Arrange
        var email = "test@gmail.com";
        _mockOtpRedisService.Setup(o => o.GetOtpTenantAsync(email, It.IsAny<string>())).ReturnsAsync("123456");

        // Act
        var result = await _authService.VerifyForgotPasswordOtpAsync(email, "123456");

        // Assert
        result.Should().NotBeNullOrEmpty();
    }
    
    [Fact]
    public async Task VerifyForgotPasswordOtpAsync_WhenOtpIsEmpty_ThrowsDomainException()
    {
        // Arrange
        var email = "test@gmail.com";
        _mockOtpRedisService.Setup(o => o.GetOtpTenantAsync(email, It.IsAny<string>()))
            .ReturnsAsync((string)null);

        // Act
        var action = async () => await _authService.VerifyForgotPasswordOtpAsync(email, "123456");

        // Assert
        await action.Should().ThrowAsync<DomainException>()
            .WithMessage(OtpMessage.OtpError.OTP_INVALID);
    }

    [Fact]
    public async Task VerifyForgotPasswordOtpAsync_WhenOtpIsWrong_ThrowsDomainException()
    {
        // Arrange
        var email = "test@gmail.com";
        _mockOtpRedisService.Setup(o => o.GetOtpTenantAsync(email, It.IsAny<string>()))
            .ReturnsAsync("654321"); 

        // Act
        var action = async () => await _authService.VerifyForgotPasswordOtpAsync(email, "123456"); // Mã nhập vào

        // Assert
        await action.Should().ThrowAsync<DomainException>()
            .WithMessage(OtpMessage.OtpError.OTP_INVALID);
    }
    
    [Fact]
    public async Task CompleteResetPasswordAsync_WhenTokenInvalid_ThrowsDomainException()
    {
        _mockRedisDb.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync("valid_token");

        var action = async () => await _authService.CompleteResetPasswordAsync("test@gmail.com", "wrong_token", "newPass");

        await action.Should().ThrowAsync<DomainException>().WithMessage(OtpMessage.OtpError.OTP_INVALID);
    }

    [Fact]
    public async Task CompleteResetPasswordAsync_WhenTenantNotFound_ThrowsDomainException()
    {
        _mockRedisDb.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync("valid_token");
        _mockUnitOfWork.Setup(u => u.Tenants.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Tenant, bool>>>(), It.IsAny<Expression<Func<Tenant, object>>[]>()))
            .ReturnsAsync((Tenant)null);

        var action = async () => await _authService.CompleteResetPasswordAsync("test@gmail.com", "valid_token", "newPass");

        await action.Should().ThrowAsync<DomainException>().WithMessage(TenantMessage.TenantError.TENANT_NOT_FOUND);
    }

    [Fact]
    public async Task CompleteResetPasswordAsync_WhenValid_UpdatesPasswordAndDeletesToken()
    {
        // Arrange
        var email = "test@gmail.com";
        var token = "guid-token";
        _mockRedisDb.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync(token);

        var tenant = new Tenant
        {
            Account = new AuthenticationUser { Email = email }
        };
        _mockUnitOfWork.Setup(u => u.Tenants.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Tenant, bool>>>(), It.IsAny<Expression<Func<Tenant, object>>[]>()))
            .ReturnsAsync(tenant);

        // Act
        var result = await _authService.CompleteResetPasswordAsync(email, token, "NewPass123");

        // Assert
        result.Should().Be(TenantMessage.TenantSuccess.TENANT_RESET_PASSWORD);
        _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        _mockRedisDb.Verify(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Once);
    }
    
    [Fact]
    public async Task CompleteResetPasswordAsync_WhenTokenExpiredOrNull_ThrowsDomainException()
    {
        // Arrange
        _mockRedisDb.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((string)null);

        // Act
        var action = async () => await _authService.CompleteResetPasswordAsync("test@gmail.com", "any_token", "newPass");

        // Assert
        await action.Should().ThrowAsync<DomainException>()
            .WithMessage(OtpMessage.OtpError.OTP_INVALID);
    }
    #endregion

    #region 5. Staff Reset/Forgot Password
    [Fact]
    public async Task ResetPasswordStaff_WhenStaffNotFound_ThrowsDomainException()
    {
        _mockUnitOfWork.Setup(u => u.Staffs.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Staff, bool>>>(), It.IsAny<Expression<Func<Staff, object>>[]>()))
            .ReturnsAsync((Staff)null);

        var action = async () => await _authService.ResetPasswordStaff(new ResetPasswordStaffRequest { Email = "s@test.com" });

        await action.Should().ThrowAsync<DomainException>().WithMessage(StaffMessage.StaffError.STAFF_NOT_FOUND);
    }

    [Fact]
    public async Task ResetPasswordStaff_WhenOldPasswordWrong_ThrowsDomainException()
    {
        var staff = new Staff
        {
            Account = new AuthenticationUser { Password = BCrypt.Net.BCrypt.HashPassword("correct_old") }
        };
        _mockUnitOfWork.Setup(u => u.Staffs.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Staff, bool>>>(), It.IsAny<Expression<Func<Staff, object>>[]>()))
            .ReturnsAsync(staff);

        var action = async () => await _authService.ResetPasswordStaff(new ResetPasswordStaffRequest
        {
            Email = "s@test.com",
            OldPassword = "wrong_old",
            NewPassword = "New"
        });

        await action.Should().ThrowAsync<DomainException>().WithMessage(AuthMessage.AuthError.ACCOUNT_WRONG_PASSWORD);
    }

    [Fact]
    public async Task ResetPasswordStaff_WhenValid_ReturnsSuccess()
    {
        var staff = new Staff
        {
            Account = new AuthenticationUser { Password = BCrypt.Net.BCrypt.HashPassword("correct_old") }
        };
        _mockUnitOfWork.Setup(u => u.Staffs.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Staff, bool>>>(), It.IsAny<Expression<Func<Staff, object>>[]>()))
            .ReturnsAsync(staff);

        var result = await _authService.ResetPasswordStaff(new ResetPasswordStaffRequest
        {
            Email = "s@test.com",
            OldPassword = "correct_old",
            NewPassword = "NewPassword123"
        });

        result.Should().Be(StaffMessage.StaffSuccess.STAFF_RESET_PASSWORD);
        _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
    }

    [Fact]
    public async Task SendForgotPasswordStaffOtpAsync_WhenStaffNotFound_ThrowsDomainException()
    {
        _mockUnitOfWork.Setup(u => u.Staffs.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Staff, bool>>>(), It.IsAny<Expression<Func<Staff, object>>[]>()))
            .ReturnsAsync((Staff)null);

        var action = async () => await _authService.SendForgotPasswordStaffOtpAsync("s@test.com");

        await action.Should().ThrowAsync<DomainException>().WithMessage(StaffMessage.StaffError.STAFF_NOT_FOUND);
    }

    [Fact]
    public async Task SendForgotPasswordStaffOtpAsync_WhenValid_ReturnsOtp()
    {
        _mockUnitOfWork.Setup(u => u.Staffs.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Staff, bool>>>(), It.IsAny<Expression<Func<Staff, object>>[]>()))
            .ReturnsAsync(new Staff());
        _mockOtpRedisService.Setup(o => o.GenerateAndSaveStaffForgotOtpAsync(It.IsAny<string>())).ReturnsAsync("123456");

        var result = await _authService.SendForgotPasswordStaffOtpAsync("s@test.com");

        result.Should().Be("123456");
    }

    [Fact]
    public async Task VerifyForgotPasswordStaffOtpAsync_WhenOtpInvalid_ThrowsDomainException()
    {
        _mockOtpRedisService.Setup(o => o.GetOtpTenantAsync(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync("111222");

        var action = async () => await _authService.VerifyForgotPasswordStaffOtpAsync("s@test.com", "wrong");

        await action.Should().ThrowAsync<DomainException>().WithMessage(OtpMessage.OtpError.OTP_INVALID);
    }

    [Fact]
    public async Task VerifyForgotPasswordStaffOtpAsync_WhenValid_ReturnsToken()
    {
        // Arrange
        var email = "staff@test.com";
        _mockOtpRedisService.Setup(o => o.GetOtpTenantAsync(email, It.IsAny<string>())).ReturnsAsync("123456");

        // Act
        var result = await _authService.VerifyForgotPasswordStaffOtpAsync(email, "123456");

        // Assert
        result.Should().NotBeNullOrEmpty();
    }
    
    [Fact]
    public async Task VerifyForgotPasswordStaffOtpAsync_WhenOtpExpiredOrNull_ThrowsDomainException()
    {
        // Arrange
        _mockOtpRedisService.Setup(o => o.GetOtpTenantAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string)null);

        // Act
        var action = async () => await _authService.VerifyForgotPasswordStaffOtpAsync("s@test.com", "123456");

        // Assert
        await action.Should().ThrowAsync<DomainException>()
            .WithMessage(OtpMessage.OtpError.OTP_INVALID);
    }

    [Fact]
    public async Task CompleteForgotPasswordStaffAsync_WhenTokenInvalid_ThrowsDomainException()
    {
        _mockRedisDb.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync("valid_token");

        var action = async () => await _authService.CompleteForgotPasswordStaffAsync(new CompleteResetPasswordRequest { Email = "s@test.com", ResetToken = "wrong_token", NewPassword = "New" });

        await action.Should().ThrowAsync<DomainException>().WithMessage(OtpMessage.OtpError.OTP_INVALID);
    }

    [Fact]
    public async Task CompleteForgotPasswordStaffAsync_WhenStaffNotFound_ThrowsDomainException()
    {
        _mockRedisDb.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync("valid_token");
        _mockUnitOfWork.Setup(u => u.Staffs.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Staff, bool>>>(), It.IsAny<Expression<Func<Staff, object>>[]>()))
            .ReturnsAsync((Staff)null);

        var action = async () => await _authService.CompleteForgotPasswordStaffAsync(new CompleteResetPasswordRequest { Email = "s@test.com", ResetToken = "valid_token", NewPassword = "New" });

        await action.Should().ThrowAsync<DomainException>().WithMessage(StaffMessage.StaffError.STAFF_NOT_FOUND);
    }

    [Fact]
    public async Task CompleteForgotPasswordStaffAsync_WhenValid_UpdatesPassword()
    {
        _mockRedisDb.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>())).ReturnsAsync("valid_token");
        var staff = new Staff { Account = new AuthenticationUser() };
        _mockUnitOfWork.Setup(u => u.Staffs.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Staff, bool>>>(), It.IsAny<Expression<Func<Staff, object>>[]>()))
            .ReturnsAsync(staff);

        var result = await _authService.CompleteForgotPasswordStaffAsync(new CompleteResetPasswordRequest { Email = "s@test.com", ResetToken = "valid_token", NewPassword = "NewPassword123" });

        result.Should().Be(StaffMessage.StaffSuccess.STAFF_RESET_PASSWORD);
        _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
        _mockRedisDb.Verify(db => db.KeyDeleteAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()), Times.Once);
    }
    
    [Fact]
    public async Task CompleteForgotPasswordStaffAsync_WhenTokenExpiredOrNull_ThrowsDomainException()
    {
        // Arrange
        _mockRedisDb.Setup(db => db.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync((string)null);

        // Act
        var action = async () => await _authService.CompleteForgotPasswordStaffAsync(new CompleteResetPasswordRequest 
        { 
            Email = "s@test.com", 
            ResetToken = "any_token", 
            NewPassword = "New" 
        });

        // Assert
        await action.Should().ThrowAsync<DomainException>()
            .WithMessage(OtpMessage.OtpError.OTP_INVALID);
    }
    #endregion
}
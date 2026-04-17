using AutoMapper;
using ClosedXML.Excel;
using FluentAssertions;
using Moq;
using ScanToOrder.Application.DTOs.Dashboard;
using ScanToOrder.Application.DTOs.External;
using ScanToOrder.Application.DTOs.Orders;
using ScanToOrder.Application.DTOs.User;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Services;
using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.Bank;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using System.Linq.Expressions;
using Xunit;
namespace ScanToOrder.Application.UnitTest.Services
{
    public class TenantServiceTests
    {
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<IMapper> _mapperMock;
        private readonly Mock<ITaxService> _taxServiceMock;
        private readonly Mock<IBankLookupService> _bankLookupServiceMock;
        private readonly Mock<IOtpRedisService> _otpRedisServiceMock;
        private readonly Mock<IAuthenticatedUserService> _authUserServiceMock;
        private readonly Mock<ITransactionRedisService> _transactionRedisServiceMock;
        private readonly Mock<IRealtimeService> _realtimeServiceMock;
        private readonly TenantService _tenantService;

        public TenantServiceTests()
        {
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _mapperMock = new Mock<IMapper>();
            _taxServiceMock = new Mock<ITaxService>();
            _bankLookupServiceMock = new Mock<IBankLookupService>();
            _otpRedisServiceMock = new Mock<IOtpRedisService>();
            _authUserServiceMock = new Mock<IAuthenticatedUserService>();
            _transactionRedisServiceMock = new Mock<ITransactionRedisService>();
            _realtimeServiceMock = new Mock<IRealtimeService>();

            _tenantService = new TenantService(
                _unitOfWorkMock.Object,
                _mapperMock.Object,
                _taxServiceMock.Object,
                _otpRedisServiceMock.Object,
                _authUserServiceMock.Object,
                _bankLookupServiceMock.Object,
                _transactionRedisServiceMock.Object,
                _realtimeServiceMock.Object
            );
        }

        [Fact]
        public async Task RegisterTenantAsync_InvalidOtp_ThrowsDomainException()
        {
            // Arrange
            var request = new RegisterTenantRequest { Email = "test@gmail.com", OtpCode = "123456", Password = "Password123!", Phone = "0123456789" };
            _otpRedisServiceMock.Setup(s => s.GetOtpTenantAsync(request.Email, It.IsAny<string>()))
                .ReturnsAsync("654321"); // OTP không khớp

            // Act
            Func<Task> act = async () => await _tenantService.RegisterTenantAsync(request);

            // Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage(OtpMessage.OtpError.OTP_INVALID);
        }

        [Fact]
        public async Task RegisterTenantAsync_ValidRequest_ReturnsSuccessMessage()
        {
            // 1. Arrange
            var request = new RegisterTenantRequest
            {
                Email = "new@gmail.com",
                OtpCode = "123456",
                Password = "Password123!",
                Phone = "0123456789"
            };

            // Khởi tạo đối tượng giả định để Mapper trả về
            var mockUser = new AuthenticationUser { Id = Guid.NewGuid() };
            var mockTenant = new Tenant { Id = Guid.NewGuid() };

            _otpRedisServiceMock.Setup(s => s.GetOtpTenantAsync(request.Email, It.IsAny<string>()))
                .ReturnsAsync("123456");

            _unitOfWorkMock.Setup(u => u.AuthenticationUsers.GetByEmailAsync(request.Email))
                .ReturnsAsync((AuthenticationUser)null);

            // QUAN TRỌNG: Đảm bảo Mapper luôn trả về object thay vì null
            _mapperMock.Setup(m => m.Map<AuthenticationUser>(It.IsAny<object>()))
                       .Returns(mockUser);

            _mapperMock.Setup(m => m.Map<Tenant>(It.IsAny<object>()))
                       .Returns(mockTenant);

            // Setup cho AuthenticationUsers Repository
            _unitOfWorkMock.Setup(u => u.AuthenticationUsers.AddAsync(It.IsAny<AuthenticationUser>()))
                .Returns(Task.CompletedTask); // Sử dụng Task.CompletedTask cho phương thức trả về Task

            // Setup cho Tenants Repository
            _unitOfWorkMock.Setup(u => u.Tenants.AddAsync(It.IsAny<Tenant>()))
                .Returns(Task.CompletedTask);

            // 2. Act
            var result = await _tenantService.RegisterTenantAsync(request);

            // 3. Assert
            result.Should().Be(TenantMessage.TenantSuccess.TENANT_REGISTERED);
            mockUser.Password.Should().NotBeNullOrEmpty(); // Kiểm tra xem password đã được hash và gán chưa
            _unitOfWorkMock.Verify(u => u.SaveAsync(), Times.Once);
        }

        [Fact]
        public async Task RegisterTenantAsync_InvalidPassword_ThrowsDomainException()
        {
            // Arrange
            var request = new RegisterTenantRequest
            {
                Email = "test@gmail.com",
                Password = "123", // Giả sử mật khẩu này không đạt chuẩn
                OtpCode = "123456",
                Phone = "0123456789"
            };

            // Act
            Func<Task> act = async () => await _tenantService.RegisterTenantAsync(request);

            // Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage(StaffMessage.StaffError.INVALID_PASSWORD);
        }

        [Fact]
        public async Task RegisterTenantAsync_TenantAlreadyExists_ThrowsDomainException()
        {
            // Arrange
            var request = new RegisterTenantRequest
            {
                Email = "existing@gmail.com",
                OtpCode = "123456",
                Password = "Password123!",
                Phone = "0123456789"
            };

            // Vượt qua bước OTP
            _otpRedisServiceMock.Setup(s => s.GetOtpTenantAsync(request.Email, It.IsAny<string>()))
                .ReturnsAsync("123456");

            // Giả lập tìm thấy User đã tồn tại trong DB
            _unitOfWorkMock.Setup(u => u.AuthenticationUsers.GetByEmailAsync(request.Email))
                .ReturnsAsync(new AuthenticationUser { Email = request.Email });

            // Act
            Func<Task> act = async () => await _tenantService.RegisterTenantAsync(request);

            // Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage(TenantMessage.TenantError.TENANT_ALREADY_EXISTS);

            // Đảm bảo không bao giờ chạy đến bước Save
            _unitOfWorkMock.Verify(u => u.SaveAsync(), Times.Never);
        }

        [Fact]
        public async Task RegisterTenantAsync_OtpNotFound_ThrowsDomainException()
        {
            // Arrange
            var request = new RegisterTenantRequest { Email = "test@gmail.com", OtpCode = "123456", Password = "Password123!", Phone = "0123456789" };

            _otpRedisServiceMock.Setup(s => s.GetOtpTenantAsync(request.Email, It.IsAny<string>()))
                .ReturnsAsync((string)null); // Giả lập OTP đã hết hạn trong Redis

            // Act
            Func<Task> act = async () => await _tenantService.RegisterTenantAsync(request);

            // Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage(OtpMessage.OtpError.OTP_INVALID);
        }

        [Fact]
        public async Task ValidationTaxCodeAsync_TaxCodeAlreadyVerified_ThrowsException()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            _authUserServiceMock.Setup(a => a.ProfileId).Returns(tenantId);
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId))
                .ReturnsAsync(new Tenant { Id = tenantId, IsVerifyTax = true });

            // Act & Assert
            await Assert.ThrowsAsync<DomainException>(() => _tenantService.ValidationTaxCodeAsync("123456789"));
        }

        [Fact]
        public async Task ValidationTaxCodeAsync_ValidTaxCode_UpdatesTenant()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var taxCode = "123456789";
            var tenant = new Tenant { Id = tenantId, IsVerifyTax = false };

            _authUserServiceMock.Setup(a => a.ProfileId).Returns(tenantId);
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(tenant);

            _unitOfWorkMock.Setup(u => u.Tenants.ExistsAsync(It.IsAny<Expression<Func<Tenant, bool>>>()))
                .ReturnsAsync(false);

            var mockTaxResponse = new TaxLookupResult
            {
                IsValid = true,
                Representative = "Công Ty A"
            };

            _taxServiceMock.Setup(t => t.GetTaxCodeDetailsAsync(taxCode))
                .ReturnsAsync(mockTaxResponse);

            // Act
            var result = await _tenantService.ValidationTaxCodeAsync(taxCode);

            // Assert
            result.Should().BeTrue();
            _unitOfWorkMock.Verify(u => u.SaveAsync(), Times.Once);
        }

        [Fact]
        public async Task ValidationTaxCodeAsync_TenantNotFound_ThrowsDomainException()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            _authUserServiceMock.Setup(a => a.ProfileId).Returns(tenantId);

            // Giả lập không tìm thấy tenant
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId))
                .ReturnsAsync((Tenant)null);

            // Act
            Func<Task> act = async () => await _tenantService.ValidationTaxCodeAsync("123456");

            // Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage(TenantMessage.TenantError.TENANT_NOT_FOUND);
        }

        [Fact]
        public async Task ValidationTaxCodeAsync_TaxCodeAlreadyExists_ThrowsDomainException()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var duplicateTaxCode = "1122334455";
            var tenant = new Tenant { Id = tenantId, IsVerifyTax = false, TaxNumber = "OLD_TAX" };

            _authUserServiceMock.Setup(a => a.ProfileId).Returns(tenantId);
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(tenant);

            // XÓA CancellationToken ở đây
            _unitOfWorkMock.Setup(u => u.Tenants.ExistsAsync(It.IsAny<Expression<Func<Tenant, bool>>>()))
                .ReturnsAsync(true);

            // Act
            Func<Task> act = async () => await _tenantService.ValidationTaxCodeAsync(duplicateTaxCode);

            // Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage(TenantMessage.TenantError.TAX_CODE_ALREADY_EXISTS);

            _taxServiceMock.Verify(t => t.GetTaxCodeDetailsAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ValidationTaxCodeAsync_TaxServiceReturnsInvalid_ReturnsFalse()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var taxCode = "999999999";
            var tenant = new Tenant { Id = tenantId, IsVerifyTax = false };

            _authUserServiceMock.Setup(a => a.ProfileId).Returns(tenantId);
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(tenant);

            // XÓA CancellationToken ở đây
            _unitOfWorkMock.Setup(u => u.Tenants.ExistsAsync(It.IsAny<Expression<Func<Tenant, bool>>>()))
                .ReturnsAsync(false);

            _taxServiceMock.Setup(t => t.GetTaxCodeDetailsAsync(taxCode))
                .ReturnsAsync(new TaxLookupResult { IsValid = false });

            // Act
            var result = await _tenantService.ValidationTaxCodeAsync(taxCode);

            // Assert
            result.Should().BeFalse();
            _unitOfWorkMock.Verify(u => u.SaveAsync(), Times.Never);
        }

        [Fact]
        public async Task UpdateBankInfoAsync_BankNotFound_ThrowsException()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            _authUserServiceMock.Setup(a => a.ProfileId).Returns(tenantId);
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant { Id = tenantId });
            _unitOfWorkMock.Setup(u => u.Banks.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Banks, bool>>>()))
                .ReturnsAsync((Banks)null);

            // Act & Assert
            await Assert.ThrowsAsync<DomainException>(() => _tenantService.UpdateBankInfoAsync(Guid.NewGuid(), "123456"));
        }

        [Fact]
        public async Task UpdateBankInfoAsync_ValidInfo_ReturnsQrUrl()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var bankId = Guid.NewGuid();
            _authUserServiceMock.Setup(a => a.ProfileId).Returns(tenantId);
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant { Id = tenantId });
            _unitOfWorkMock.Setup(u => u.Banks.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Banks, bool>>>()))
                .ReturnsAsync(new Banks { Id = bankId, Code = "VCB", ShortName = "Vietcombank" });

            _bankLookupServiceMock.Setup(b => b.LookupAccountAsync(It.IsAny<BankLookRequest>()))
                .ReturnsAsync(new BankLookResponse { Success = true });

            // Act
            var result = await _tenantService.UpdateBankInfoAsync(bankId, "10122334455");

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain("https://qr.sepay.vn");
            _transactionRedisServiceMock.Verify(r => r.SaveTransactionCodeAsync(It.IsAny<string>(), tenantId), Times.Once);
        }

        [Fact]
        public async Task VerifyBankAccountAsync_InvalidBankAccount_ThrowsDomainException()
        {
            // Arrange
            var paymentCode = "CODE";
            _transactionRedisServiceMock.Setup(r => r.GetTenantIdByTransactionCodeAsync(paymentCode))
                .ReturnsAsync(Guid.NewGuid().ToString());

            // QUAN TRỌNG: Phải trả về một object Bank thay vì để mặc định (null)
            var bank = new Banks { Code = "970436", ShortName = "VCB" };

            // Lưu ý: Dùng đúng Signature có tham số string (hoặc CancellationToken) như đã thảo luận
            _unitOfWorkMock.Setup(u => u.Banks.FirstOrDefaultAsync(
                    It.IsAny<Expression<Func<Banks, bool>>>(),
                    It.IsAny<string>())) // Hoặc It.IsAny<CancellationToken>() tùy vào Interface của bạn
                .ReturnsAsync(bank);

            // Giả lập API ngân hàng trả về Success = false
            _bankLookupServiceMock.Setup(b => b.LookupAccountAsync(It.IsAny<BankLookRequest>()))
                .ReturnsAsync(new BankLookResponse { Success = false });

            // Act & Assert
            var act = () => _tenantService.VerifyBankAccountAsync(paymentCode, "VCB", "123");

            await act.Should().ThrowAsync<DomainException>()
                     .WithMessage("Thông tin tài khoản ngân hàng không hợp lệ");
        }

        [Fact]
        public async Task UpdateBankInfoAsync_ValidData_ReturnsQrUrl()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var bankId = Guid.NewGuid();
            var accountNumber = "123456789";
            var bank = new Banks { Id = bankId, Code = "970436", ShortName = "VCB" };
            var tenant = new Tenant { Id = tenantId, IsVerifyBank = false };

            _authUserServiceMock.Setup(a => a.ProfileId).Returns(tenantId);
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(tenant);

            // Mock GetByFieldsIncludeAsync (Lưu ý: dùng đúng tham số string hoặc token nếu cần)
            _unitOfWorkMock.Setup(u => u.Banks.GetByFieldsIncludeAsync(
                It.IsAny<Expression<Func<Banks, bool>>>(),
                It.IsAny<Expression<Func<Banks, object>>[]>())) // Sử dụng mảng []
                .ReturnsAsync(bank);

            _bankLookupServiceMock.Setup(b => b.LookupAccountAsync(It.IsAny<BankLookRequest>()))
                .ReturnsAsync(new BankLookResponse { Success = true });

            // Act
            var result = await _tenantService.UpdateBankInfoAsync(bankId, accountNumber);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain(accountNumber);
            _unitOfWorkMock.Verify(u => u.SaveAsync(), Times.Once);
            _transactionRedisServiceMock.Verify(r => r.SaveTransactionCodeAsync(It.IsAny<string>(), tenantId), Times.Once);
        }

        [Fact]
        public async Task UpdateBankInfoAsync_AlreadyVerified_ThrowsDomainException()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            _authUserServiceMock.Setup(a => a.ProfileId).Returns(tenantId);
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId))
                .ReturnsAsync(new Tenant { Id = tenantId, IsVerifyBank = true });

            // Act
            Func<Task> act = async () => await _tenantService.UpdateBankInfoAsync(Guid.NewGuid(), "123");

            // Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage("Không thể cập nhật thông tin ngân hàng khi đã xác thực*");
        }

        [Fact]
        public async Task UpdateBankInfoAsync_BankNotFound_ThrowsDomainException()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            _authUserServiceMock.Setup(a => a.ProfileId).Returns(tenantId);
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant { Id = tenantId });

            // Giả lập không tìm thấy bank
            _unitOfWorkMock.Setup(u => u.Banks.GetByFieldsIncludeAsync(
                It.IsAny<Expression<Func<Banks, bool>>>(),
                It.IsAny<Expression<Func<Banks, object>>[]>())) // Sử dụng mảng []
                .ReturnsAsync((Banks)null); // Trả về null hoặc một giá trị không hợp lệ để giả lập bank không tồn tại

            // Act & Assert
            await Assert.ThrowsAsync<DomainException>(() =>
                _tenantService.UpdateBankInfoAsync(Guid.NewGuid(), "123"));
        }

        [Fact]
        public async Task UpdateBankInfoAsync_InvalidAccountNumber_ThrowsDomainException()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var bankId = Guid.NewGuid();
            _authUserServiceMock.Setup(a => a.ProfileId).Returns(tenantId);
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant { Id = tenantId });
            _unitOfWorkMock.Setup(u => u.Banks.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Banks, bool>>>(), It.IsAny<Expression<Func<Banks, object>>[]>()))
                .ReturnsAsync(new Banks { Code = "VCB" });

            // Giả lập lookup thất bại
            _bankLookupServiceMock.Setup(b => b.LookupAccountAsync(It.IsAny<BankLookRequest>()))
                .ReturnsAsync(new BankLookResponse { Success = false });

            // Act & Assert
            var act = () => _tenantService.UpdateBankInfoAsync(bankId, "invalid_acc");

            await act.Should().ThrowAsync<DomainException>()
                .WithMessage("Thông tin tài khoản ngân hàng không hợp lệ");
        }

        [Fact]
        public async Task UpdateBankInfoAsync_TenantNotFound_ThrowsDomainException()
        {
            // Arrange
            _authUserServiceMock.Setup(a => a.ProfileId).Returns(Guid.NewGuid());
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Tenant)null);

            // Act & Assert
            await Assert.ThrowsAsync<DomainException>(() =>
                _tenantService.UpdateBankInfoAsync(Guid.NewGuid(), "123"));
        }

        [Fact]
        public async Task VerifyBankAccountAsync_ValidData_ReturnsTrueAndUpdatesTenant()
        {
            // Arrange
            var paymentCode = "PAY123";
            var gateway = "VCB";
            var accountNumber = "123456789";
            var tenantIdStr = Guid.NewGuid().ToString();
            var tenantId = Guid.Parse(tenantIdStr);

            var bank = new Banks { Code = "970436", ShortName = gateway };
            var tenant = new Tenant
            {
                Id = tenantId,
                Name = "Nguyễn Văn A", // Tên có dấu trong DB
                CardNumber = accountNumber,
                BankId = Guid.NewGuid(),
                IsVerifyBank = false
            };

            _transactionRedisServiceMock.Setup(r => r.GetTenantIdByTransactionCodeAsync(paymentCode))
                .ReturnsAsync(tenantIdStr);

            // Mock FirstOrDefaultAsync với CancellationToken (như đã xử lý ở các phần trước)
            _unitOfWorkMock.Setup(u => u.Banks.FirstOrDefaultAsync(
                    It.IsAny<Expression<Func<Banks, bool>>>(),
                    It.IsAny<string>()))
                .ReturnsAsync(bank);

            _bankLookupServiceMock.Setup(b => b.LookupAccountAsync(It.IsAny<BankLookRequest>()))
                .ReturnsAsync(new BankLookResponse
                {
                    Success = true,
                    Data = new BankLookData { OwnerName = "NGUYEN VAN A" } // Tên từ ngân hàng (thường không dấu)
                });

            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId))
                .ReturnsAsync(tenant);

            // Act
            var result = await _tenantService.VerifyBankAccountAsync(paymentCode, gateway, accountNumber);

            // Assert
            result.Should().BeTrue();
            tenant.IsVerifyBank.Should().BeTrue();
            tenant.IsVerifyTax.Should().BeTrue(); // Vì "Nguyễn Văn A" -> "nguyen van a" khớp với Bank

            _unitOfWorkMock.Verify(u => u.SaveAsync(), Times.Once);
            _transactionRedisServiceMock.Verify(r => r.DeleteTransactionCodeAsync(paymentCode), Times.Once);
            _realtimeServiceMock.Verify(s => s.NotifyTenantProfileChanged(tenantIdStr), Times.Once);
        }

        [Fact]
        public async Task VerifyBankAccountAsync_TenantMissingBankInfo_ThrowsDomainException()
        {
            // Arrange
            var tenantIdStr = Guid.NewGuid().ToString();
            var tenant = new Tenant { Id = Guid.Parse(tenantIdStr), CardNumber = null }; // Chưa cập nhật STK

            _transactionRedisServiceMock.Setup(r => r.GetTenantIdByTransactionCodeAsync(It.IsAny<string>()))
                .ReturnsAsync(tenantIdStr);

            _unitOfWorkMock.Setup(u => u.Banks.FirstOrDefaultAsync(It.IsAny<Expression<Func<Banks, bool>>>(), It.IsAny<string>()))
                .ReturnsAsync(new Banks { Code = "VCB" });

            _bankLookupServiceMock.Setup(b => b.LookupAccountAsync(It.IsAny<BankLookRequest>()))
                .ReturnsAsync(new BankLookResponse { Success = true, Data = new BankLookData() });

            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(tenant);

            // Act & Assert
            var act = () => _tenantService.VerifyBankAccountAsync("CODE", "VCB", "123");

            await act.Should().ThrowAsync<DomainException>()
                .WithMessage("Thông tin ngân hàng chưa được cập nhật");
        }

        [Fact]
        public async Task VerifyBankAccountAsync_PaymentCodeExpired_ReturnsFalse()
        {
            // Arrange
            _transactionRedisServiceMock.Setup(r => r.GetTenantIdByTransactionCodeAsync(It.IsAny<string>()))
                .ReturnsAsync((string)null); // Redis không tìm thấy mã

            _unitOfWorkMock.Setup(u => u.Banks.FirstOrDefaultAsync(It.IsAny<Expression<Func<Banks, bool>>>(), It.IsAny<string>()))
                .ReturnsAsync(new Banks { Code = "VCB" });

            _bankLookupServiceMock.Setup(b => b.LookupAccountAsync(It.IsAny<BankLookRequest>()))
                .ReturnsAsync(new BankLookResponse { Success = true, Data = new BankLookData() });

            // Act
            var result = await _tenantService.VerifyBankAccountAsync("EXPIRED_CODE", "VCB", "123");

            // Assert
            result.Should().BeFalse();
            _unitOfWorkMock.Verify(u => u.SaveAsync(), Times.Never);
        }

        [Fact]
        public async Task VerifyBankAccountAsync_TenantNotFound_ThrowsDomainException()
        {
            // Arrange
            var paymentCode = "VALID_CODE";
            var tenantIdStr = Guid.NewGuid().ToString();

            _transactionRedisServiceMock.Setup(r => r.GetTenantIdByTransactionCodeAsync(paymentCode))
                .ReturnsAsync(tenantIdStr);

            _unitOfWorkMock.Setup(u => u.Banks.FirstOrDefaultAsync(It.IsAny<Expression<Func<Banks, bool>>>(), It.IsAny<string>()))
                .ReturnsAsync(new Banks { Code = "VCB" });

            _bankLookupServiceMock.Setup(b => b.LookupAccountAsync(It.IsAny<BankLookRequest>()))
                .ReturnsAsync(new BankLookResponse { Success = true, Data = new BankLookData() });

            // Giả lập không tìm thấy Tenant trong DB
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>()))
                .ReturnsAsync((Tenant)null);

            // Act & Assert
            var act = () => _tenantService.VerifyBankAccountAsync(paymentCode, "VCB", "123");

            await act.Should().ThrowAsync<DomainException>()
                .WithMessage(TenantMessage.TenantError.TENANT_NOT_FOUND);
        }

        [Fact]
        public async Task VerifyBankAccountAsync_NameNotMatch_OnlyVerifyBankTrue()
        {
            // Arrange
            var paymentCode = "PAY123";
            var tenantIdStr = Guid.NewGuid().ToString();
            var tenant = new Tenant
            {
                Id = Guid.Parse(tenantIdStr),
                Name = "Nguyễn Văn A",
                CardNumber = "123",
                BankId = Guid.NewGuid(),
                IsVerifyTax = false,
                IsVerifyBank = false
            };

            _transactionRedisServiceMock.Setup(r => r.GetTenantIdByTransactionCodeAsync(paymentCode))
                .ReturnsAsync(tenantIdStr);
            _unitOfWorkMock.Setup(u => u.Banks.FirstOrDefaultAsync(It.IsAny<Expression<Func<Banks, bool>>>(), It.IsAny<string>()))
                .ReturnsAsync(new Banks { Code = "VCB" });
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(tenant);

            // Giả lập tên từ ngân hàng trả về là "TRAN THI B" (Không khớp NGUYEN VAN A)
            _bankLookupServiceMock.Setup(b => b.LookupAccountAsync(It.IsAny<BankLookRequest>()))
                .ReturnsAsync(new BankLookResponse
                {
                    Success = true,
                    Data = new BankLookData { OwnerName = "TRAN THI B" }
                });

            // Act
            await _tenantService.VerifyBankAccountAsync(paymentCode, "VCB", "123");

            // Assert
            tenant.IsVerifyBank.Should().BeTrue();
            tenant.IsVerifyTax.Should().BeFalse(); // QUAN TRỌNG: Tax phải vẫn là false
        }

        [Fact]
        public async Task VerifyBankAccountAsync_MissingBankId_ThrowsDomainException()
        {
            // Arrange
            var tenant = new Tenant
            {
                Id = Guid.NewGuid(),
                CardNumber = "123456",
                BankId = null // Thiếu BankId
            };

            _transactionRedisServiceMock.Setup(r => r.GetTenantIdByTransactionCodeAsync(It.IsAny<string>()))
                .ReturnsAsync(Guid.NewGuid().ToString());
            _unitOfWorkMock.Setup(u => u.Banks.FirstOrDefaultAsync(It.IsAny<Expression<Func<Banks, bool>>>(), It.IsAny<string>()))
                .ReturnsAsync(new Banks { Code = "VCB" });
            _bankLookupServiceMock.Setup(b => b.LookupAccountAsync(It.IsAny<BankLookRequest>()))
                .ReturnsAsync(new BankLookResponse { Success = true, Data = new BankLookData() });
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(tenant);

            // Act & Assert
            var act = () => _tenantService.VerifyBankAccountAsync("CODE", "VCB", "123");
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage("Thông tin ngân hàng chưa được cập nhật");
        }

        [Fact]
        public async Task VerifyBankAccountAsync_BankGatewayNotFound_ThrowsException()
        {
            // Arrange
            _transactionRedisServiceMock.Setup(r => r.GetTenantIdByTransactionCodeAsync(It.IsAny<string>()))
                .ReturnsAsync(Guid.NewGuid().ToString());

            // Giả lập không tìm thấy bank theo gateway
            _unitOfWorkMock.Setup(u => u.Banks.FirstOrDefaultAsync(It.IsAny<Expression<Func<Banks, bool>>>(), It.IsAny<string>()))
                .ReturnsAsync((Banks)null);

            // Act & Assert
            // Case này hiện tại sẽ ném ra NullReferenceException do code service chưa check null bank
            await Assert.ThrowsAsync<NullReferenceException>(() =>
                _tenantService.VerifyBankAccountAsync("CODE", "INVALID_GATEWAY", "123"));
        }

        [Fact]
        public async Task GetAllTenantsAsync_WhenTenantsExist_ReturnsTenantDtoList()
        {
            // Arrange
            var tenants = new List<Tenant>
    {
        new Tenant { Id = Guid.NewGuid(), Name = "Tenant 1" },
        new Tenant { Id = Guid.NewGuid(), Name = "Tenant 2" }
    };

            var tenantDtos = new List<TenantDto>
    {
        new TenantDto { Id = tenants[0].Id, Name = "Tenant 1" },
        new TenantDto { Id = tenants[1].Id, Name = "Tenant 2" }
    };

            _unitOfWorkMock.Setup(u => u.Tenants.GetTenantsWithSubscriptionsAsync())
                .ReturnsAsync(tenants);

            _mapperMock.Setup(m => m.Map<IEnumerable<TenantDto>>(tenants))
                .Returns(tenantDtos);

            // Act
            var result = await _tenantService.GetAllTenantsAsync();

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.First().Name.Should().Be("Tenant 1");
            _unitOfWorkMock.Verify(u => u.Tenants.GetTenantsWithSubscriptionsAsync(), Times.Once);
        }

        [Fact]
        public async Task GetAllTenantsAsync_WhenNoTenantsExist_ReturnsEmptyList()
        {
            // Arrange
            var emptyTenants = new List<Tenant>();

            _unitOfWorkMock.Setup(u => u.Tenants.GetTenantsWithSubscriptionsAsync())
                .ReturnsAsync(emptyTenants);

            _mapperMock.Setup(m => m.Map<IEnumerable<TenantDto>>(emptyTenants))
                .Returns(new List<TenantDto>());

            // Act
            var result = await _tenantService.GetAllTenantsAsync();

            // Assert
            result.Should().BeEmpty();
            _unitOfWorkMock.Verify(u => u.Tenants.GetTenantsWithSubscriptionsAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateTenantStatusAsync_ValidData_ReturnsTrue()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var account = new AuthenticationUser { Id = Guid.NewGuid(), IsActive = false }; // Đang bị block
            var tenant = new Tenant { Id = tenantId, Account = account };

            _unitOfWorkMock.Setup(u => u.Tenants.GetByFieldsIncludeAsync(
                    It.IsAny<Expression<Func<Tenant, bool>>>(),
                    It.IsAny<Expression<Func<Tenant, object>>[]>()))
                .ReturnsAsync(tenant);

            // Act
            var result = await _tenantService.UpdateTenantStatusAsync(tenantId, true); // Kích hoạt lại

            // Assert
            result.Should().BeTrue();
            account.IsActive.Should().BeTrue();
            _unitOfWorkMock.Verify(u => u.SaveAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateTenantStatusAsync_TenantNotFound_ThrowsDomainException()
        {
            // Arrange
            _unitOfWorkMock.Setup(u => u.Tenants.GetByFieldsIncludeAsync(
                    It.IsAny<Expression<Func<Tenant, bool>>>(),
                    It.IsAny<Expression<Func<Tenant, object>>[]>()))
                .ReturnsAsync((Tenant)null);

            // Act & Assert
            var act = () => _tenantService.UpdateTenantStatusAsync(Guid.NewGuid(), true);

            await act.Should().ThrowAsync<DomainException>()
                .WithMessage(TenantMessage.TenantError.TENANT_NOT_FOUND);
        }

        [Fact]
        public async Task UpdateTenantStatusAsync_AlreadyActive_ThrowsDomainException()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var tenant = new Tenant
            {
                Id = tenantId,
                Account = new AuthenticationUser { IsActive = true }
            };

            _unitOfWorkMock.Setup(u => u.Tenants.GetByFieldsIncludeAsync(
                    It.IsAny<Expression<Func<Tenant, bool>>>(),
                    It.IsAny<Expression<Func<Tenant, object>>[]>()))
                .ReturnsAsync(tenant);

            // Act & Assert
            var act = () => _tenantService.UpdateTenantStatusAsync(tenantId, true);

            await act.Should().ThrowAsync<DomainException>()
                .WithMessage(TenantMessage.TenantError.TENANT_ALREADY_ACTIVE);
        }

        [Fact]
        public async Task UpdateTenantStatusAsync_AlreadyBlocked_ThrowsDomainException()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var tenant = new Tenant
            {
                Id = tenantId,
                Account = new AuthenticationUser { IsActive = false }
            };

            _unitOfWorkMock.Setup(u => u.Tenants.GetByFieldsIncludeAsync(
                    It.IsAny<Expression<Func<Tenant, bool>>>(),
                    It.IsAny<Expression<Func<Tenant, object>>[]>()))
                .ReturnsAsync(tenant);

            // Act & Assert
            var act = () => _tenantService.UpdateTenantStatusAsync(tenantId, false);

            await act.Should().ThrowAsync<DomainException>()
                .WithMessage(TenantMessage.TenantError.TENANT_ALREADY_BLOCKED);
        }

        [Fact]
        public async Task UpdateTenantAsync_ValidRequest_ReturnsSuccessMessage()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var request = new UpdateTenantDtoRequest
            {
                TaxNumber = "123456789",
                BankId = Guid.NewGuid(),
                CardNumber = "123456789"
            };
            var tenant = new Tenant { Id = tenantId, TaxNumber = "OLD_TAX", BankId = Guid.NewGuid() };

            _authUserServiceMock.Setup(a => a.ProfileId).Returns(tenantId);
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(tenant);

            // Mock check trùng MST
            _unitOfWorkMock.Setup(u => u.Tenants.ExistsAsync(It.IsAny<Expression<Func<Tenant, bool>>>()))
                .ReturnsAsync(false);

            // Mock API Tax
            _taxServiceMock.Setup(t => t.GetTaxCodeDetailsAsync(request.TaxNumber))
                .ReturnsAsync(new TaxLookupResult { IsValid = true, Representative = "Công Ty Mới" });

            // Mock check tồn tại Bank
            _unitOfWorkMock.Setup(u => u.Banks.ExistsAsync(It.IsAny<Expression<Func<Banks, bool>>>()))
                .ReturnsAsync(true);

            // Act
            var result = await _tenantService.UpdateTenantAsync(request);

            // Assert
            result.Should().Be(TenantMessage.TenantSuccess.TENANT_UPDATED);
            tenant.Name.Should().Be("Công Ty Mới");
            _unitOfWorkMock.Verify(u => u.SaveAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateTenantAsync_TaxCodeAlreadyExists_ThrowsDomainException()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var request = new UpdateTenantDtoRequest { TaxNumber = "DUPLICATE_TAX", BankId = Guid.NewGuid(), CardNumber = "123456789" };
            var tenant = new Tenant { Id = tenantId, TaxNumber = "OLD_TAX" };

            _authUserServiceMock.Setup(a => a.ProfileId).Returns(tenantId);
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(tenant);

            _unitOfWorkMock.Setup(u => u.Tenants.ExistsAsync(It.IsAny<Expression<Func<Tenant, bool>>>()))
                .ReturnsAsync(true); // Giả lập trùng mã

            // Act & Assert
            var act = () => _tenantService.UpdateTenantAsync(request);
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage(TenantMessage.TenantError.TAX_CODE_ALREADY_EXISTS);
        }

        [Fact]
        public async Task UpdateTenantAsync_TaxCodeInvalid_ThrowsDomainException()
        {
            // Arrange
            var request = new UpdateTenantDtoRequest { TaxNumber = "INVALID_TAX", BankId = Guid.NewGuid(), CardNumber = "123456789" };
            var tenant = new Tenant { Id = Guid.NewGuid(), TaxNumber = "OLD_TAX" };

            _authUserServiceMock.Setup(a => a.ProfileId).Returns(tenant.Id);
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenant.Id)).ReturnsAsync(tenant);
            _unitOfWorkMock.Setup(u => u.Tenants.ExistsAsync(It.IsAny<Expression<Func<Tenant, bool>>>())).ReturnsAsync(false);

            _taxServiceMock.Setup(t => t.GetTaxCodeDetailsAsync(request.TaxNumber))
                .ReturnsAsync(new TaxLookupResult { IsValid = false });

            // Act & Assert
            var act = () => _tenantService.UpdateTenantAsync(request);
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage(TenantMessage.TenantError.TAX_CODE_INVALID);
        }

        [Fact]
        public async Task UpdateTenantAsync_BankNotFound_ThrowsDomainException()
        {
            // 1. Arrange
            var tenantId = Guid.NewGuid();
            var bankId = Guid.NewGuid();

            // Đảm bảo TaxNumber cũ và mới GIỐNG NHAU để code bỏ qua nhánh If của Tax
            var request = new UpdateTenantDtoRequest
            {
                BankId = bankId,
                CardNumber = "123456789",
                TaxNumber = "123456789"
            };

            var tenant = new Tenant
            {
                Id = tenantId,
                BankId = Guid.NewGuid(),
                TaxNumber = "123456789" // Giống request
            };

            _authUserServiceMock.Setup(a => a.ProfileId).Returns(tenantId);
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(tenant);

            // Mock cho nhánh Bank: Trả về false để ném lỗi
            _unitOfWorkMock.Setup(u => u.Banks.ExistsAsync(It.IsAny<Expression<Func<Banks, bool>>>()))
                .ReturnsAsync(false);

            // Mock Mapper (Dùng cho phương thức Map(source, dest))
            _mapperMock.Setup(m => m.Map(It.IsAny<UpdateTenantDtoRequest>(), It.IsAny<Tenant>()))
                       .Returns(tenant);

            // 2. Act
            var act = () => _tenantService.UpdateTenantAsync(request);

            // 3. Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage(BankMessage.BankError.BANK_NOT_FOUND);
        }

        [Fact]
        public async Task UpdateTenantAsync_TenantNotFound_ThrowsNotFoundException()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            _authUserServiceMock.Setup(a => a.ProfileId).Returns(tenantId);
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync((Tenant)null);

            // Act & Assert
            var request = new UpdateTenantDtoRequest
            {
                TaxNumber = "123",
                CardNumber = "456"
            };

            await Assert.ThrowsAsync<NotFoundException>(() =>
                _tenantService.UpdateTenantAsync(request));
        }

        [Fact]
        public async Task GetTenantByIdAsync_WhenTenantExists_ReturnsTenantDto()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var tenant = new Tenant
            {
                Id = tenantId,
                Name = "Công Ty ABC",
                Account = new AuthenticationUser { Email = "admin@abc.com" },
                Bank = new Banks { ShortName = "VCB" }
            };
            var tenantDto = new TenantDto { Id = tenantId, Name = "Công Ty ABC" };

            // Mock GetByFieldsIncludeAsync với mảng Expression cho Includes
            _unitOfWorkMock.Setup(u => u.Tenants.GetByFieldsIncludeAsync(
                    It.IsAny<Expression<Func<Tenant, bool>>>(),
                    It.IsAny<Expression<Func<Tenant, object>>[]>()))
                .ReturnsAsync(tenant);

            _mapperMock.Setup(m => m.Map<TenantDto>(tenant))
                .Returns(tenantDto);

            // Act
            var result = await _tenantService.GetTenantByIdAsync(tenantId);

            // Assert
            result.Should().NotBeNull();
            result.Id.Should().Be(tenantId);
            result.Name.Should().Be("Công Ty ABC");
            _unitOfWorkMock.Verify(u => u.Tenants.GetByFieldsIncludeAsync(It.IsAny<Expression<Func<Tenant, bool>>>(), It.IsAny<Expression<Func<Tenant, object>>[]>()), Times.Once);
        }

        [Fact]
        public async Task GetTenantByIdAsync_WhenTenantDoesNotExist_ThrowsDomainException()
        {
            // Arrange
            var tenantId = Guid.NewGuid();

            // Giả lập trả về null
            _unitOfWorkMock.Setup(u => u.Tenants.GetByFieldsIncludeAsync(
                    It.IsAny<Expression<Func<Tenant, bool>>>(),
                    It.IsAny<Expression<Func<Tenant, object>>[]>()))
                .ReturnsAsync((Tenant)null);

            // Act
            Func<Task> act = async () => await _tenantService.GetTenantByIdAsync(tenantId);

            // Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage(TenantMessage.TenantError.TENANT_NOT_FOUND);
        }

        [Fact]
        public async Task GetTotalRevenueByTenantAsync_TenantNotFound_ThrowsDomainException()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync((Tenant)null);

            // Act & Assert
            var act = () => _tenantService.GetTotalRevenueByTenantAsync(tenantId, null, null, "alltime");
            await act.Should().ThrowAsync<DomainException>().WithMessage(TenantMessage.TenantError.TENANT_NOT_FOUND);
        }

        [Fact]
        public async Task GetTotalRevenueByTenantAsync_InvalidPreset_ThrowsDomainException()
        {
            // Act & Assert
            var act = () => _tenantService.GetTotalRevenueByTenantAsync(Guid.NewGuid(), null, null, "invalid_preset");
            await act.Should().ThrowAsync<DomainException>().WithMessage("preset không hợp lệ.*");
        }

        [Fact]
        public async Task GetTotalRevenueByTenantAsync_OnlyOneDateProvided_ThrowsDomainException()
        {
            // Act & Assert (Chỉ truyền startDate mà không truyền endDate)
            var act = () => _tenantService.GetTotalRevenueByTenantAsync(Guid.NewGuid(), DateTime.Now, null, null);
            await act.Should().ThrowAsync<DomainException>().WithMessage("Khi lọc theo ngày, cần truyền đủ cả startDate và endDate.");
        }

        [Fact]
        public async Task GetTotalRevenueByTenantAsync_CustomDateRangeValid_ReturnsCorrectFilterInfo()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var startDate = new DateTime(2023, 1, 1);
            var endDate = new DateTime(2023, 1, 10);
            var revenueFromDb = new List<(int RestaurantId, int TotalOrders, decimal GrossRevenue, decimal NetRevenue, decimal TotalDiscount)>
            {
                (1, 2, 200m, 180m, 20m)
            };
                _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(new Tenant { Id = tenantId });
                _unitOfWorkMock.Setup(u => u.Restaurants.GetRestaurantsWithSubscriptionsByTenantIdAsync(tenantId))
                    .ReturnsAsync(new List<Restaurant>());
                _unitOfWorkMock.Setup(u => u.Orders.GetRevenueByTenantAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>()))
                .ReturnsAsync(revenueFromDb);
                        _mapperMock.Setup(m => m.Map<List<TenantRestaurantRevenueDto>>(It.IsAny<object>()))
                            .Returns(new List<TenantRestaurantRevenueDto>());

            // Act
            var result = await _tenantService.GetTotalRevenueByTenantAsync(tenantId, startDate, endDate, null);

            // Assert
            result.FilterPreset.Should().Be("custom");
            result.IsAllTime.Should().BeFalse();
            result.StartDate.Should().Be(startDate);
            result.EndDate.Should().Be(endDate);
        }

        [Fact]
        public async Task GetTotalRevenueByTenantAsync_ValidData_CalculatesCorrectTotals()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var resId1 = 12;
            var resId2 = 13;

            var tenant = new Tenant { Id = tenantId, Name = "Tenant Test" };
            var restaurants = new List<Restaurant> {
                new() { Id = resId1, Slug = "slug1" },
                new() { Id = resId2, Slug = "slug2" }
            };

                    // FIX: Định nghĩa đúng kiểu Tuple mà Interface yêu cầu
                    var revenueFromDb = new List<(int RestaurantId, int TotalOrders, decimal GrossRevenue, decimal NetRevenue, decimal TotalDiscount)>
            {
                (resId1, 2, 200m, 180m, 20m)
            };

                    var restaurantDtos = new List<TenantRestaurantRevenueDto>
            {
                new() { RestaurantId = resId1 },
                new() { RestaurantId = resId2 }
            };

            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(tenant);
            _unitOfWorkMock.Setup(u => u.Restaurants.GetRestaurantsWithSubscriptionsByTenantIdAsync(tenantId)).ReturnsAsync(restaurants);

            // FIX: Setup khớp với kiểu Tuple
            _unitOfWorkMock.Setup(u => u.Orders.GetRevenueByTenantAsync(tenantId, null, null))
                .ReturnsAsync(revenueFromDb);

            _mapperMock.Setup(m => m.Map<List<TenantRestaurantRevenueDto>>(restaurants)).Returns(restaurantDtos);

            // Act
            var result = await _tenantService.GetTotalRevenueByTenantAsync(tenantId, null, null, "alltime");

            // Assert
            result.TotalRestaurants.Should().Be(2);
            result.TotalOrders.Should().Be(2);
            result.GrossRevenue.Should().Be(200);
            result.NetRevenue.Should().Be(180);
            result.AverageOrderValue.Should().Be(90);

            var res2Dto = result.Restaurants.First(x => x.RestaurantId == resId2);
            res2Dto.TotalOrders.Should().Be(0);
        }

        [Theory]
        [InlineData("today")]
        [InlineData("last7days")]
        [InlineData("last30days")]
        [InlineData("thismonth")]
        [InlineData("thisyear")]
        public async Task GetTotalRevenueByTenantAsync_ValidPresets_ReturnsCorrectDates(string preset)
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var tenant = new Tenant { Id = tenantId, Name = "Test Tenant" };

            // 1. Mock Tenant
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId))
                .ReturnsAsync(tenant);

            // 2. Mock Restaurants (Phải trả về List trống, không được để null)
            _unitOfWorkMock.Setup(u => u.Restaurants.GetRestaurantsWithSubscriptionsByTenantIdAsync(tenantId))
                .ReturnsAsync(new List<Restaurant>());

            // 3. Mock Orders Revenue (Phải trả về List Tuple trống)
            var emptyRevenueList = new List<(int RestaurantId, int TotalOrders, decimal GrossRevenue, decimal NetRevenue, decimal TotalDiscount)>();
            _unitOfWorkMock.Setup(u => u.Orders.GetRevenueByTenantAsync(tenantId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(emptyRevenueList);

            // 4. Mock Mapper
            _mapperMock.Setup(m => m.Map<List<TenantRestaurantRevenueDto>>(It.IsAny<object>()))
                .Returns(new List<TenantRestaurantRevenueDto>());

            // Act
            var result = await _tenantService.GetTotalRevenueByTenantAsync(tenantId, null, null, preset);

            // Assert
            result.Should().NotBeNull();
            result.FilterPreset.ToLower().Should().Be(preset.ToLower());
            _unitOfWorkMock.Verify(u => u.Orders.GetRevenueByTenantAsync(tenantId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()), Times.Once);
        }

        [Fact]
        public async Task GetTotalRevenueByTenantAsync_InvalidPresetName_ThrowsDomainException()
        {
            // Act & Assert
            var act = () => _tenantService.GetTotalRevenueByTenantAsync(Guid.NewGuid(), null, null, "unsupported_preset");

            await act.Should().ThrowAsync<DomainException>()
                .WithMessage("preset không hợp lệ*");
        }

        [Fact]
        public async Task GetTotalRevenueByTenantAsync_NoDatesNoPreset_ReturnsAllTime()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var tenant = new Tenant { Id = tenantId, Name = "AllTime Tenant" };

            // 1. Mock Tenant (Bắt buộc phải có để không bị chặn ở dòng check null tenant)
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId))
                .ReturnsAsync(tenant);

            // 2. Mock Restaurants (Phải trả về List rỗng thay vì null)
            _unitOfWorkMock.Setup(u => u.Restaurants.GetRestaurantsWithSubscriptionsByTenantIdAsync(tenantId))
                .ReturnsAsync(new List<Restaurant>());

            // 3. Mock Orders (Phải trả về List Tuple rỗng thay vì null)
            var emptyRevenue = new List<(int RestaurantId, int TotalOrders, decimal GrossRevenue, decimal NetRevenue, decimal TotalDiscount)>();
            _unitOfWorkMock.Setup(u => u.Orders.GetRevenueByTenantAsync(tenantId, null, null))
                .ReturnsAsync(emptyRevenue);

            // 4. Mock Mapper
            _mapperMock.Setup(m => m.Map<List<TenantRestaurantRevenueDto>>(It.IsAny<object>()))
                .Returns(new List<TenantRestaurantRevenueDto>());

            // Act: Truyền null cho dates và preset
            var result = await _tenantService.GetTotalRevenueByTenantAsync(tenantId, null, null, null);

            // Assert
            result.FilterPreset.Should().Be("allTime");
            result.IsAllTime.Should().BeTrue();
            result.StartDate.Should().BeNull();
            result.EndDate.Should().BeNull();
        }

        [Fact]
        public async Task GetTotalRevenueByTenantAsync_EndDateBeforeStartDate_ThrowsDomainException()
        {
            // Arrange: EndDate (1/1) trước StartDate (10/1)
            var start = new DateTime(2023, 1, 10);
            var end = new DateTime(2023, 1, 1);

            // Act & Assert
            var act = () => _tenantService.GetTotalRevenueByTenantAsync(Guid.NewGuid(), start, end, null);
            await act.Should().ThrowAsync<DomainException>().WithMessage("endDate phải lớn hơn hoặc bằng startDate.");
        }

        [Fact]
        public async Task GetTotalRevenueByTenantAsync_ValidCustomRange_ReturnsCustomPreset()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var start = new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2023, 1, 30, 0, 0, 0, DateTimeKind.Utc);

            var tenant = new Tenant { Id = tenantId, Name = "Custom Range Tenant" };

            // 1. Mock Tenant (Để không bị ném lỗi TENANT_NOT_FOUND)
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId))
                .ReturnsAsync(tenant);

            // 2. Mock Restaurants (Tránh Null khi Mapping)
            _unitOfWorkMock.Setup(u => u.Restaurants.GetRestaurantsWithSubscriptionsByTenantIdAsync(tenantId))
                .ReturnsAsync(new List<Restaurant>());

            // 3. Mock Orders (Quan trọng: Phải đúng kiểu Tuple và trả về List rỗng thay vì null)
            var emptyRevenue = new List<(int RestaurantId, int TotalOrders, decimal GrossRevenue, decimal NetRevenue, decimal TotalDiscount)>();
            _unitOfWorkMock.Setup(u => u.Orders.GetRevenueByTenantAsync(tenantId, start, end))
                .ReturnsAsync(emptyRevenue);

            // 4. Mock Mapper (Để list Restaurants sau khi map không bị null)
            _mapperMock.Setup(m => m.Map<List<TenantRestaurantRevenueDto>>(It.IsAny<object>()))
                .Returns(new List<TenantRestaurantRevenueDto>());

            // Act
            var result = await _tenantService.GetTotalRevenueByTenantAsync(tenantId, start, end, null);

            // Assert
            result.Should().NotBeNull();
            result.FilterPreset.Should().Be("custom");
            result.IsAllTime.Should().BeFalse();
            result.StartDate.Should().Be(start);
            result.EndDate.Should().Be(end);
        }

        [Fact]
        public async Task GetTotalRevenueByTenantAsync_RangeTooLong_ThrowsDomainException()
        {
            // Arrange: Khoảng cách 2 năm
            var start = new DateTime(2023, 1, 1);
            var end = new DateTime(2025, 1, 1);

            // Act & Assert
            var act = () => _tenantService.GetTotalRevenueByTenantAsync(Guid.NewGuid(), start, end, null);
            await act.Should().ThrowAsync<DomainException>().WithMessage("Khoảng thời gian tối đa là 366 ngày.*");
        }

        [Fact]
        public async Task GetTotalRevenueByTenantAsync_NoTenantIdProvided_UsesProfileId()
        {
            // Arrange
            var profileId = Guid.NewGuid();
            var tenant = new Tenant { Id = profileId, Name = "Test Tenant" };

            _authUserServiceMock.Setup(a => a.ProfileId).Returns(profileId);
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(profileId)).ReturnsAsync(tenant);

            // Mock Restaurants trả về list rỗng
            _unitOfWorkMock.Setup(u => u.Restaurants.GetRestaurantsWithSubscriptionsByTenantIdAsync(profileId))
                .ReturnsAsync(new List<Restaurant>());

            // FIX TẠI ĐÂY: Khai báo đúng kiểu Tuple mà Interface yêu cầu
            var emptyRevenueList = new List<(int RestaurantId, int TotalOrders, decimal GrossRevenue, decimal NetRevenue, decimal TotalDiscount)>();

            _unitOfWorkMock.Setup(u => u.Orders.GetRevenueByTenantAsync(profileId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(emptyRevenueList); // Truyền đúng list Tuple vào đây

            _mapperMock.Setup(m => m.Map<List<TenantRestaurantRevenueDto>>(It.IsAny<object>()))
                .Returns(new List<TenantRestaurantRevenueDto>());

            // Act
            var result = await _tenantService.GetTotalRevenueByTenantAsync(null, null, null, "alltime");

            // Assert
            result.TenantId.Should().Be(profileId);
            _authUserServiceMock.Verify(a => a.ProfileId, Times.AtLeastOnce);
            _unitOfWorkMock.Verify(u => u.Orders.GetRevenueByTenantAsync(profileId, null, null), Times.Once);
        }

        [Fact]
        public async Task ToggleTenantStatusAsync_ValidTenant_ReturnsTrue()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var isSuspended = true;
            var tenant = new Tenant { Id = tenantId, Account = new AuthenticationUser() };

            // Mock tìm thấy Tenant
            _unitOfWorkMock.Setup(u => u.Tenants.GetByFieldsIncludeAsync(
                    It.IsAny<Expression<Func<Tenant, bool>>>(),
                    It.IsAny<Expression<Func<Tenant, object>>[]>()))
                .ReturnsAsync(tenant);

            // Mock Repository thực hiện suspend thành công
            _unitOfWorkMock.Setup(u => u.Tenants.SuspendTenantAsync(tenantId, isSuspended))
                .ReturnsAsync(true);

            // Act
            var result = await _tenantService.ToggleTenantStatusAsync(tenantId, isSuspended);

            // Assert
            result.Should().BeTrue();
            _unitOfWorkMock.Verify(u => u.Tenants.SuspendTenantAsync(tenantId, isSuspended), Times.Once);
        }

        [Fact]
        public async Task ToggleTenantStatusAsync_TenantNotFound_ThrowsDomainException()
        {
            // Arrange
            var tenantId = Guid.NewGuid();

            // Giả lập Repository trả về null
            _unitOfWorkMock.Setup(u => u.Tenants.GetByFieldsIncludeAsync(
                    It.IsAny<Expression<Func<Tenant, bool>>>(),
                    It.IsAny<Expression<Func<Tenant, object>>[]>()))
                .ReturnsAsync((Tenant)null);

            // Act
            Func<Task> act = async () => await _tenantService.ToggleTenantStatusAsync(tenantId, true);

            // Assert
            await act.Should().ThrowAsync<DomainException>()
                .WithMessage(TenantMessage.TenantError.TENANT_NOT_FOUND);

            // Đảm bảo không gọi xuống hàm Suspend nếu không tìm thấy Tenant
            _unitOfWorkMock.Verify(u => u.Tenants.SuspendTenantAsync(It.IsAny<Guid>(), It.IsAny<bool>()), Times.Never);
        }

        [Fact]
        public async Task UpdateTenantAsync_TaxNumberNotChanged_SkipsTaxValidation()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var sameTaxNumber = "123456789";
            var request = new UpdateTenantDtoRequest
            {
                TaxNumber = sameTaxNumber,
                CardNumber = "999",
                BankId = Guid.Empty
            };

            var tenant = new Tenant { Id = tenantId, TaxNumber = sameTaxNumber };

            _authUserServiceMock.Setup(a => a.ProfileId).Returns(tenantId);
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(tenant);

            // Act
            await _tenantService.UpdateTenantAsync(request);

            // Assert
            // Kiểm tra xem ExistsAsync của Tenants có KHÔNG được gọi không
            _unitOfWorkMock.Verify(u => u.Tenants.ExistsAsync(It.IsAny<Expression<Func<Tenant, bool>>>()), Times.Never);
            _taxServiceMock.Verify(t => t.GetTaxCodeDetailsAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task UpdateTenantAsync_BankIdNotChanged_SkipsBankValidation()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var sameBankId = Guid.NewGuid();
            var request = new UpdateTenantDtoRequest
            {
                TaxNumber = "123", // Giống cũ để bỏ qua if trên
                CardNumber = "999",
                BankId = sameBankId
            };

            var tenant = new Tenant { Id = tenantId, TaxNumber = "123", BankId = sameBankId };

            _authUserServiceMock.Setup(a => a.ProfileId).Returns(tenantId);
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(tenant);

            // Act
            await _tenantService.UpdateTenantAsync(request);

            // Assert
            // Kiểm tra xem ExistsAsync của Banks có KHÔNG được gọi không
            _unitOfWorkMock.Verify(u => u.Banks.ExistsAsync(It.IsAny<Expression<Func<Banks, bool>>>()), Times.Never);
        }

        [Fact]
        public async Task UpdateTenantAsync_TaxNumberEmpty_SkipsTaxValidation()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var request = new UpdateTenantDtoRequest { TaxNumber = "", CardNumber = "999" };
            var tenant = new Tenant { Id = tenantId, TaxNumber = "123" };

            _authUserServiceMock.Setup(a => a.ProfileId).Returns(tenantId);
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(tenant);

            // Act
            await _tenantService.UpdateTenantAsync(request);

            // Assert
            _taxServiceMock.Verify(t => t.GetTaxCodeDetailsAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task GetTotalRevenueByTenantAsync_NoTenantIdAndNoProfileId_ThrowsDomainException()
        {
            // Arrange
            _authUserServiceMock.Setup(a => a.ProfileId).Returns((Guid?)null);

            // Act & Assert
            var act = () => _tenantService.GetTotalRevenueByTenantAsync(null, null, null, "alltime");

            await act.Should().ThrowAsync<DomainException>()
                .WithMessage("Không xác định được tenant hiện tại.");
        }

        [Fact]
        public async Task GetTotalRevenueByTenantAsync_RestaurantWithZeroOrders_SetsAverageToZero()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var resId = 10;
            var tenant = new Tenant { Id = tenantId };
            var restaurants = new List<Restaurant> { new() { Id = resId, Slug = "slug-slug" } };

            // Doanh thu có TotalOrders = 0
            var revenueFromDb = new List<(int RestaurantId, int TotalOrders, decimal GrossRevenue, decimal NetRevenue, decimal TotalDiscount)>
            {
                (resId, 0, 0m, 0m, 0m)
            };

            var restaurantDtos = new List<TenantRestaurantRevenueDto> { new() { RestaurantId = resId } };

            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(tenant);
            _unitOfWorkMock.Setup(u => u.Restaurants.GetRestaurantsWithSubscriptionsByTenantIdAsync(tenantId)).ReturnsAsync(restaurants);
            _unitOfWorkMock.Setup(u => u.Orders.GetRevenueByTenantAsync(tenantId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>())).ReturnsAsync(revenueFromDb);
            _mapperMock.Setup(m => m.Map<List<TenantRestaurantRevenueDto>>(restaurants)).Returns(restaurantDtos);

            // Act
            var result = await _tenantService.GetTotalRevenueByTenantAsync(tenantId, null, null, "alltime");

            // Assert
            result.Restaurants.First().AverageOrderValue.Should().Be(0);
        }

        [Fact]
        public async Task UpdateTenantAsync_BankIdIsEmpty_SkipsBankValidation()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var request = new UpdateTenantDtoRequest
            {
                TaxNumber = "123", // Giống cũ để bỏ qua if trên
                CardNumber = "999",
                BankId = Guid.Empty // Kích hoạt nhánh False của điều kiện Bank
            };

            var tenant = new Tenant { Id = tenantId, TaxNumber = "123", BankId = Guid.NewGuid() };

            _authUserServiceMock.Setup(a => a.ProfileId).Returns(tenantId);
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(tenant);

            // Act
            await _tenantService.UpdateTenantAsync(request);

            // Assert
            // Kiểm tra xem ExistsAsync của Banks KHÔNG được gọi
            _unitOfWorkMock.Verify(u => u.Banks.ExistsAsync(It.IsAny<Expression<Func<Banks, bool>>>()), Times.Never);
        }

        [Fact]
        public async Task UpdateTenantAsync_BankIdHasNotChanged_SkipsBankValidation()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var currentBankId = Guid.NewGuid();
            var request = new UpdateTenantDtoRequest
            {
                TaxNumber = "123",
                CardNumber = "999",
                BankId = currentBankId // Trùng với BankId hiện tại
            };

            var tenant = new Tenant { Id = tenantId, TaxNumber = "123", BankId = currentBankId };

            _authUserServiceMock.Setup(a => a.ProfileId).Returns(tenantId);
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(tenant);

            // Act
            await _tenantService.UpdateTenantAsync(request);

            // Assert
            _unitOfWorkMock.Verify(u => u.Banks.ExistsAsync(It.IsAny<Expression<Func<Banks, bool>>>()), Times.Never);
        }

        [Fact]
        public async Task UpdateTenantAsync_BankIdIsProvidedButNotChanged_SkipsValidationAndStaysGreen()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var existingBankId = Guid.NewGuid(); // ID ngân hàng hiện tại

            var request = new UpdateTenantDtoRequest
            {
                TaxNumber = "123", // Giống cũ để bypass if trên
                CardNumber = "999",
                BankId = existingBankId // TRÙNG VỚI HIỆN TẠI
            };

            var tenant = new Tenant
            {
                Id = tenantId,
                TaxNumber = "123",
                BankId = existingBankId
            };

            _authUserServiceMock.Setup(a => a.ProfileId).Returns(tenantId);
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(tenant);

            // Mock Mapper và Save để hàm chạy đến cuối
            _mapperMock.Setup(m => m.Map(It.IsAny<UpdateTenantDtoRequest>(), It.IsAny<Tenant>())).Returns(tenant);

            // Act
            await _tenantService.UpdateTenantAsync(request);

            // Assert
            // Quan trọng: Kiểm tra xem ExistsAsync của Banks KHÔNG bao giờ được gọi
            _unitOfWorkMock.Verify(u => u.Banks.ExistsAsync(It.IsAny<Expression<Func<Banks, bool>>>()), Times.Never);
        }

        [Fact]
        public async Task UpdateTenantAsync_BankIdIsNewButSameAsOld_FullBranchCoverage()
        {
            // Arrange
            var tenantId = Guid.NewGuid();
            var existingBankId = Guid.NewGuid(); // Một Guid thực thụ, không phải Empty

            var request = new UpdateTenantDtoRequest
            {
                TaxNumber = "123", // Giống cũ để bypass if phía trên
                CardNumber = "999",
                BankId = existingBankId // TRUYỀN VÀO GIÁ TRỊ GIỐNG HỆT CŨ
            };

            var tenant = new Tenant
            {
                Id = tenantId,
                TaxNumber = "123",
                BankId = existingBankId // Database đang lưu đúng ID này
            };

            _authUserServiceMock.Setup(a => a.ProfileId).Returns(tenantId);
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId)).ReturnsAsync(tenant);
            _mapperMock.Setup(m => m.Map(It.IsAny<UpdateTenantDtoRequest>(), It.IsAny<Tenant>())).Returns(tenant);

            // Act
            await _tenantService.UpdateTenantAsync(request);

            // Assert
            // Verify rằng nó KHÔNG chạy vào trong IF (không check ExistsAsync)
            _unitOfWorkMock.Verify(u => u.Banks.ExistsAsync(It.IsAny<Expression<Func<Banks, bool>>>()), Times.Never);
        }

        [Theory]
        [InlineData("today")]
        [InlineData("last7days")]
        [InlineData("thismonth")] // Lưu ý: Code của bạn dùng ToLowerInvariant() nên "thismonth" là chuẩn
        public async Task GetTotalRevenue_WithValidPresets_ShouldReturnCorrectFilter(string preset)
        {
            // 1. Arrange
            var tenantId = Guid.NewGuid();
            var tenant = new Tenant { Id = tenantId, Name = "Test Tenant" };

            // Mock Tenant: Để không bị lỗi ở dòng 273 và 274
            _unitOfWorkMock.Setup(u => u.Tenants.GetByIdAsync(tenantId))
                .ReturnsAsync(tenant);

            // Mock Restaurants: Để không bị lỗi ở dòng 277
            _unitOfWorkMock.Setup(u => u.Restaurants.GetRestaurantsWithSubscriptionsByTenantIdAsync(tenantId))
                .ReturnsAsync(new List<Restaurant>());

            // Mock Orders: Để không bị lỗi ở dòng 280 (Sử dụng đúng kiểu Tuple)
            var emptyRevenue = new List<(int RestaurantId, int TotalOrders, decimal GrossRevenue, decimal NetRevenue, decimal TotalDiscount)>();
            _unitOfWorkMock.Setup(u => u.Orders.GetRevenueByTenantAsync(tenantId, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync(emptyRevenue);

            // Mock Mapper: Để không bị lỗi ở dòng 285
            _mapperMock.Setup(m => m.Map<List<TenantRestaurantRevenueDto>>(It.IsAny<object>()))
                .Returns(new List<TenantRestaurantRevenueDto>());

            // 2. Act
            var result = await _tenantService.GetTotalRevenueByTenantAsync(tenantId, null, null, preset);

            // 3. Assert
            result.FilterPreset.ToLower().Should().Be(preset.ToLower());
        }

        [Fact]
        public async Task ResolveFilter_StartDateGreaterThanEndDate_ShouldThrowException()
        {
            var start = DateTime.UtcNow;
            var end = start.AddDays(-1);

            var act = () => _tenantService.GetTotalRevenueByTenantAsync(Guid.NewGuid(), start, end, null);

            await act.Should().ThrowAsync<DomainException>().WithMessage("endDate phải lớn hơn hoặc bằng startDate.");
        }
    }
}
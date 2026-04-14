using AutoMapper;
using FluentAssertions;
using Moq;
using ScanToOrder.Application.DTOs.Configuration;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Services;
using ScanToOrder.Application.Template;
using ScanToOrder.Application.DTOs.User;
using ScanToOrder.Domain.Entities.Configuration; 
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using System.Linq.Expressions;

namespace ScanToOrder.Application.UnitTest.Services
{
    public class ConfigurationServiceTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IEmailService> _mockEmailService;
        private readonly Mock<ITenantService> _mockTenantService;
        private readonly ConfigurationService _service;

        public ConfigurationServiceTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
            _mockMapper = new Mock<IMapper>();
            _mockEmailService = new Mock<IEmailService>();
            _mockTenantService = new Mock<ITenantService>();
            _service = new ConfigurationService(_mockUnitOfWork.Object, _mockMapper.Object, _mockEmailService.Object, _mockTenantService.Object);
        }

        #region 1. GetConfigurationsAsync

        [Fact]
        public async Task GetConfigurationsAsync_WhenDataExists_ReturnsMappedDto()
        {
            // Arrange
            var configs = new List<Configurations> { new Configurations { Id = 1, CommissionRate = 10 } };
            var expectedResponse = new ConfigurationResponse { Id = 1, CommissionRate = 10 };

            _mockUnitOfWork.Setup(u => u.Configurations.GetAllAsync(
                It.IsAny<Expression<Func<Configurations, bool>>>(),
                It.IsAny<Expression<Func<Configurations, object>>[]>()))
                .ReturnsAsync(configs);

            _mockMapper.Setup(m => m.Map<ConfigurationResponse?>(configs.First()))
                .Returns(expectedResponse);

            // Act
            var result = await _service.GetConfigurationsAsync();

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(1);
            _mockMapper.Verify(m => m.Map<ConfigurationResponse?>(It.IsAny<Configurations>()), Times.Once);
        }

        [Fact]
        public async Task GetConfigurationsAsync_WhenNoData_ReturnsNull()
        {
            // Arrange
            _mockUnitOfWork.Setup(u => u.Configurations.GetAllAsync(
                It.IsAny<Expression<Func<Configurations, bool>>>(),
                It.IsAny<Expression<Func<Configurations, object>>[]>()))
                .ReturnsAsync(new List<Configurations>());

            _mockMapper.Setup(m => m.Map<ConfigurationResponse?>(null))
                .Returns((ConfigurationResponse?)null);

            // Act
            var result = await _service.GetConfigurationsAsync();

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region 2. UpdateConfigurationsAsync

        [Fact]
        public async Task UpdateConfigurationsAsync_WhenNotFound_ThrowsDomainException()
        {
            // Arrange
            int id = 99;
            var request = new UpdateConfigurationRequest { CommissionRate = 15 };

            _mockUnitOfWork.Setup(u => u.Configurations.GetByIdAsync(id))
                .ReturnsAsync((Configurations?)null);

            // Act
            Func<Task> action = async () => await _service.UpdateConfigurationsAsync(id, request);

            // Assert
            await action.Should().ThrowAsync<DomainException>()
                .WithMessage("*Không tìm thấy cấu hình*");
        }

        [Fact]
        public async Task UpdateConfigurationsAsync_WhenFound_UpdatesAndReturnsDto()
        {
            // Arrange
            int id = 1;
            var request = new UpdateConfigurationRequest { CommissionRate = 20 };
            var existingConfig = new Configurations { Id = id, CommissionRate = 5 };
            var expectedResponse = new ConfigurationResponse { Id = id, CommissionRate = 20 };

            _mockUnitOfWork.Setup(u => u.Configurations.GetByIdAsync(id))
                .ReturnsAsync(existingConfig);

            _mockMapper.Setup(m => m.Map<ConfigurationResponse>(existingConfig))
                .Returns(expectedResponse);
            
            _mockTenantService.Setup(s => s.GetAllTenantsAsync())
                .ReturnsAsync(Array.Empty<TenantDto>());

            // Act
            var result = await _service.UpdateConfigurationsAsync(id, request);

            // Assert
            result.Should().NotBeNull();
            result.CommissionRate.Should().Be(20);
            existingConfig.CommissionRate.Should().Be(20);
            existingConfig.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(10));

            _mockUnitOfWork.Verify(u => u.Configurations.Update(existingConfig), Times.Once);
            _mockUnitOfWork.Verify(u => u.SaveAsync(), Times.Once);
            _mockEmailService.Verify(
                e => e.SendEmailsWithTemplateIdDomainAsync(
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<object>()),
                Times.Never);
        }

        [Fact]
        public async Task UpdateConfigurationsAsync_WhenTenantsHaveEmails_SendsEmailToAllNonEmptyEmails()
        {
            // Arrange
            int id = 1;
            var request = new UpdateConfigurationRequest { CommissionRate = 25 };
            var existingConfig = new Configurations { Id = id, CommissionRate = 5 };
            var expectedResponse = new ConfigurationResponse { Id = id, CommissionRate = 25 };

            _mockUnitOfWork.Setup(u => u.Configurations.GetByIdAsync(id))
                .ReturnsAsync(existingConfig);

            _mockMapper.Setup(m => m.Map<ConfigurationResponse>(existingConfig))
                .Returns(expectedResponse);

            var tenants = new List<TenantDto>
            {
                new() { Id = Guid.NewGuid(), Email = "a@test.com" },
                new() { Id = Guid.NewGuid(), Email = "" },
                new() { Id = Guid.NewGuid(), Email = "b@test.com" }
            };

            _mockTenantService.Setup(s => s.GetAllTenantsAsync())
                .ReturnsAsync(tenants);

            List<string>? capturedEmails = null;
            object? capturedTemplateData = null;

            _mockEmailService
                .Setup(e => e.SendEmailsWithTemplateIdDomainAsync(
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<object>()))
                .Callback<IEnumerable<string>, string, string, object>((emails, _, _, data) =>
                {
                    capturedEmails = emails.ToList();
                    capturedTemplateData = data;
                })
                .ReturnsAsync(true);

            // Act
            var result = await _service.UpdateConfigurationsAsync(id, request);

            // Assert
            result.CommissionRate.Should().Be(25);

            _mockEmailService.Verify(e => e.SendEmailsWithTemplateIdDomainAsync(
                    It.Is<IEnumerable<string>>(l => l.SequenceEqual(new[] { "a@test.com", "b@test.com" })),
                    EmailMessage.EmailSubject.UPDATE_CONFIGURATION_SUBJECT,
                    ResendTemplate.UPDATE_CONFIGURATION_TEMPLATE_ID,
                    It.IsAny<object>()),
                Times.Once);

            capturedEmails.Should().NotBeNull();
            capturedEmails!.Should().Equal("a@test.com", "b@test.com");

            capturedTemplateData.Should().NotBeNull();
            var commissionRateProp = capturedTemplateData!.GetType().GetProperty("CommissionRate");
            commissionRateProp.Should().NotBeNull();
            commissionRateProp!.GetValue(capturedTemplateData).Should().Be(25);
        }

        [Fact]
        public async Task UpdateConfigurationsAsync_WhenNoTenantEmail_DoesNotSendEmail()
        {
            // Arrange
            int id = 1;
            var request = new UpdateConfigurationRequest { CommissionRate = 30 };
            var existingConfig = new Configurations { Id = id, CommissionRate = 5 };
            var expectedResponse = new ConfigurationResponse { Id = id, CommissionRate = 30 };

            _mockUnitOfWork.Setup(u => u.Configurations.GetByIdAsync(id))
                .ReturnsAsync(existingConfig);

            _mockMapper.Setup(m => m.Map<ConfigurationResponse>(existingConfig))
                .Returns(expectedResponse);

            var tenants = new List<TenantDto>
            {
                new() { Id = Guid.NewGuid(), Email = "" }
            };

            _mockTenantService.Setup(s => s.GetAllTenantsAsync())
                .ReturnsAsync(tenants);

            // Act
            var result = await _service.UpdateConfigurationsAsync(id, request);

            // Assert
            result.CommissionRate.Should().Be(30);

            _mockEmailService.Verify(
                e => e.SendEmailsWithTemplateIdDomainAsync(
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<object>()),
                Times.Never);
        }

        #endregion
    }
}
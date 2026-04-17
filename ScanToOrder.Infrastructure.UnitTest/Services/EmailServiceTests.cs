using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Infrastructure.Configuration;
using ScanToOrder.Infrastructure.Services;
using System.Net;

namespace ScanToOrder.Infrastructure.UnitTest.Services
{
    public class EmailServiceTests
    {
        private readonly Mock<IOptionsSnapshot<EmailSettings>> _mockEmailOptions;
        private readonly Mock<ILogger<EmailService>> _mockLogger;
        private readonly LocalFakeHttpMessageHandler _fakeHandler;
        private readonly EmailService _service;

        public EmailServiceTests()
        {
            _mockEmailOptions = new Mock<IOptionsSnapshot<EmailSettings>>();
            _mockLogger = new Mock<ILogger<EmailService>>();
            _fakeHandler = new LocalFakeHttpMessageHandler();

            var httpClient = new HttpClient(_fakeHandler);

            var ioSettings = new EmailSettings { FromEmail = "no-reply@s2o.io", ApiKey = "io-key", ApiUrl = "http://api.io", ToEmail = "admin@s2o.io" };
            var idSettings = new EmailSettings { FromEmail = "no-reply@s2o.id", ApiKey = "id-key", ApiUrl = "http://api.id", ToEmail = "admin@s2o.id" };

            _mockEmailOptions.Setup(x => x.Get(EmailMessage.EmailDomain.IO_DOMAIN)).Returns(ioSettings);
            _mockEmailOptions.Setup(x => x.Get(EmailMessage.EmailDomain.ID_DOMAIN)).Returns(idSettings);

            _service = new EmailService(httpClient, _mockEmailOptions.Object, _mockLogger.Object);
        }

        #region 1. Standard Email Tests (Io, Id, Guest)

        [Fact]
        public async Task SendEmailViaIoDomainAsync_Success_ReturnsTrue()
        {
            // Arrange
            _fakeHandler.Handler = req => new HttpResponseMessage(HttpStatusCode.OK);

            // Act
            var result = await _service.SendEmailViaIoDomainAsync("test@gmail.com", "Sub", "Content");

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task SendEmailViaIdDomainAsync_Success_ReturnsTrue()
        {
            // Arrange
            _fakeHandler.Handler = req => new HttpResponseMessage(HttpStatusCode.OK);

            // Act
            var result = await _service.SendEmailViaIdDomainAsync("test@gmail.com", "Sub", "Content");

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task SendEmailViaIdDomainAsync_Failure_ThrowsDomainException()
        {
            // Arrange
            _fakeHandler.Handler = req => new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("Error details")
            };

            // Act
            Func<Task> act = () => _service.SendEmailViaIdDomainAsync("test@gmail.com", "Sub", "Content");

            // Assert
            await act.Should().ThrowAsync<DomainException>();
        }

        [Fact]
        public async Task GuestSendEmailAsync_Success_ReturnsTrue()
        {
            // Arrange
            _fakeHandler.Handler = req => new HttpResponseMessage(HttpStatusCode.OK);

            // Act
            var result = await _service.GuestSendEmailAsync("guest@gmail.com", "Support", "Need help");

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task GuestSendEmailAsync_Failure_ThrowsDomainException()
        {
            // Arrange
            _fakeHandler.Handler = req => new HttpResponseMessage(HttpStatusCode.InternalServerError);

            // Act
            Func<Task> act = () => _service.GuestSendEmailAsync("guest@gmail.com", "Support", "Need help");

            // Assert
            await act.Should().ThrowAsync<DomainException>();
        }

        [Fact]
        public async Task SendEmailAsync_Success_ReturnsTrue()
        {
            // Arrange
            _fakeHandler.Handler = req => new HttpResponseMessage(HttpStatusCode.OK);

            // Act
            var result = await _service.SendEmailAsync("user@gmail.com", "Hello", "Body");

            // Assert
            result.Should().BeTrue();
        }

        #endregion

        #region 2. Template Email Tests

        [Fact]
        public async Task SendEmailWithTemplateIdDomainAsync_Success_ReturnsTrue()
        {
            // Arrange
            _fakeHandler.Handler = req => new HttpResponseMessage(HttpStatusCode.OK);

            // Act
            var result = await _service.SendEmailWithTemplateIdDomainAsync("user@id.vn", "Sub", "temp-123", new { name = "Dat" });

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task SendEmailWithTemplateIdDomainAsync_Failure_ThrowsDomainException()
        {
            // Arrange
            _fakeHandler.Handler = req => new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("Invalid API Key") };

            // Act
            Func<Task> act = () => _service.SendEmailWithTemplateIdDomainAsync("user@id.vn", "Sub", "temp-123", new { name = "Dat" });

            // Assert
            await act.Should().ThrowAsync<DomainException>();
        }

        [Fact]
        public async Task SendEmailsWithTemplateIdDomainAsync_Success_ReturnsTrue()
        {
            // Arrange
            _fakeHandler.Handler = req => new HttpResponseMessage(HttpStatusCode.OK);
            var recipients = new List<string> { "u1@gmail.com", "u2@gmail.com" };

            // Act
            var result = await _service.SendEmailsWithTemplateIdDomainAsync(recipients, "Bulk", "bulk-1", new { promo = "10%" });

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task SendEmailsWithTemplateIdDomainAsync_Failure_ThrowsDomainException()
        {
            // Arrange
            _fakeHandler.Handler = req => new HttpResponseMessage(HttpStatusCode.Forbidden);
            var recipients = new List<string> { "u1@gmail.com" };

            // Act
            Func<Task> act = () => _service.SendEmailsWithTemplateIdDomainAsync(recipients, "Bulk", "bulk-1", new { promo = "10%" });

            // Assert
            await act.Should().ThrowAsync<DomainException>();
        }

        [Fact]
        public async Task SendEmailWithTemplateIoDomainAsync_Success_ReturnsTrue()
        {
            // Arrange
            _fakeHandler.Handler = req => new HttpResponseMessage(HttpStatusCode.OK);

            // Act
            var result = await _service.SendEmailWithTemplateIoDomainAsync("user@io.vn", "Sub", "temp-io", new { code = "123" });

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task SendEmailWithTemplateIoDomainAsync_Failure_ThrowsDomainException()
        {
            // Arrange
            _fakeHandler.Handler = req => new HttpResponseMessage(HttpStatusCode.NotFound);

            // Act
            Func<Task> act = () => _service.SendEmailWithTemplateIoDomainAsync("user@io.vn", "Sub", "temp-io", new { code = "123" });

            // Assert
            await act.Should().ThrowAsync<DomainException>();
        }

        #endregion

        // Helper class để Mock HttpClient
        private class LocalFakeHttpMessageHandler : HttpMessageHandler
        {
            public Func<HttpRequestMessage, HttpResponseMessage> Handler { get; set; } = req => new HttpResponseMessage(HttpStatusCode.OK);

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(Handler(request));
            }
        }
    }
}
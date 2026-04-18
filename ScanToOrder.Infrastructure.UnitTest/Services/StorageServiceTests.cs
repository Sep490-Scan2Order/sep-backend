using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Infrastructure.Configuration;
using ScanToOrder.Infrastructure.Services;
using System.Net;
using System.Net.Http;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ScanToOrder.Infrastructure.UnitTest.Services
{
    public class StorageServiceTests
    {
        private readonly Mock<ISupabaseStorageService> _mockStorageService;
        private readonly Mock<IOptions<VpsSettings>> _mockVpsOptions;
        private readonly Mock<IOptions<OpenAiSettings>> _mockOpenAiOptions;
        private readonly LocalFakeHttpMessageHandler _fakeHandler;
        private readonly StorageService _sut;

        public StorageServiceTests()
        {
            _mockStorageService = new Mock<ISupabaseStorageService>();

            _mockVpsOptions = new Mock<IOptions<VpsSettings>>();
            _mockVpsOptions.Setup(o => o.Value).Returns(new VpsSettings
            {
                VpsBaseUrl = "http://vps.com/",
                UploadApiUrl = "http://vps.com/upload"
            });

            _mockOpenAiOptions = new Mock<IOptions<OpenAiSettings>>();
            _mockOpenAiOptions.Setup(o => o.Value).Returns(new OpenAiSettings
            {
                ApiKey = "key",
                SpeechUrl = "http://openai.com/speech"
            });

            _fakeHandler = new LocalFakeHttpMessageHandler();
            var httpClient = new HttpClient(_fakeHandler);

            _sut = new StorageService(
                _mockVpsOptions.Object,
                _mockOpenAiOptions.Object,
                httpClient,
                _mockStorageService.Object);
        }

        #region 1. UploadFromBytesAsync Tests

        [Theory]
        [InlineData(null)]
        [InlineData(new byte[0])]
        public async Task UploadFromBytes_InvalidBytes_ThrowsDomainException(byte[] bytes)
        {
            // Act
            Func<Task> act = () => _sut.UploadFromBytesAsync(bytes, "test.png");

            // Assert
            await act.Should().ThrowAsync<DomainException>();
        }

        [Fact]
        public async Task UploadFromBytes_ValidBytes_ReturnsUrl()
        {
            // Arrange
            _mockStorageService.Setup(s => s.GetPublicUrl(It.IsAny<string>(), It.IsAny<string>())).Returns("http://pub.url");

            // Act
            var result = await _sut.UploadFromBytesAsync(new byte[] { 1 }, "test.png");

            // Assert
            result.Should().Be("http://pub.url");
        }

        [Fact]
        public async Task UploadFromBytes_WhenSupabaseThrows_ThrowsDomainException()
        {
            // Arrange
            _mockStorageService.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Network error"));

            // Act
            Func<Task> act = () => _sut.UploadFromBytesAsync(new byte[] { 1 }, "test.png");

            // Assert
            await act.Should().ThrowAsync<DomainException>().WithMessage("*");
        }

        [Fact]
        public async Task UploadPaymentProof_ReturnsUrl()
        {
            // Act & Assert
            _mockStorageService.Setup(s => s.GetPublicUrl(It.IsAny<string>(), It.IsAny<string>())).Returns("http://pub.url");
            var result = await _sut.UploadPaymentProofAsync(new byte[] { 1 }, "test.png");
            result.Should().Be("http://pub.url");
        }

        [Theory]
        [InlineData(null)]
        [InlineData(new byte[0])]
        public async Task UploadOrderQr_WhenBytesInvalid_ThrowsDomainException(byte[] bytes)
        {
            await _sut.Invoking(s => s.UploadOrderQrAsync(bytes, Guid.NewGuid()))
                .Should().ThrowAsync<DomainException>().WithMessage("QR code rỗng.");
        }

        [Fact]
        public async Task UploadOrderQr_WhenSupabaseThrows_ThrowsDomainException()
        {
            // Arrange
            _mockStorageService.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Database connection failed"));

            // Act
            Func<Task> act = () => _sut.UploadOrderQrAsync(new byte[] { 1 }, Guid.NewGuid());

            // Assert
            await act.Should().ThrowAsync<DomainException>();
        }

        [Fact]
        public async Task UploadPaymentProof_WhenUploadFails_ThrowsException()
        {
            // Arrange
            _mockStorageService.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Bucket not found"));

            // Act
            Func<Task> act = () => _sut.UploadPaymentProofAsync(new byte[] { 1 }, "test.png");

            // Assert
            await act.Should().ThrowAsync<DomainException>();
        }
        #endregion

        #region 2. Audio Generation & Get Logic
        [Fact]
        public async Task GetOrGenerateScanAudio_FileNotExists_GeneratesAndUploads()
        {
            // Arrange
            _fakeHandler.Handler = req =>
            {
                if (req.Method == HttpMethod.Head) return new HttpResponseMessage(HttpStatusCode.NotFound);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1, 2, 3 }) };
            };

            // Act
            var result = await _sut.GetOrGenerateScanAudioAsync(10, "Generate new scan audio");

            // Assert
            result.Should().Contain("scan_10.mp3");
            _mockStorageService.Verify(s => s.GetPublicUrl(It.IsAny<string>(), It.IsAny<string>()), Times.Never); 
        }

        [Fact]
        public async Task GetOrGenerateOrderAudio_FileExists_ReturnsUrlDirectly()
        {
            // Arrange
            _fakeHandler.Handler = req => new HttpResponseMessage(HttpStatusCode.OK);

            // Act
            var result = await _sut.GetOrGenerateOrderAudioAsync(123, "Hello");

            // Assert
            result.Should().Contain("order_123.mp3");
        }

        [Fact]
        public async Task GetOrGenerateOrderAudio_FileNotExists_GeneratesAndUploads()
        {
            // Arrange
            _fakeHandler.Handler = req => {
                if (req.Method == HttpMethod.Head) return new HttpResponseMessage(HttpStatusCode.NotFound);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1, 2, 3 }) };
            };

            // Act
            var result = await _sut.GetOrGenerateOrderAudioAsync(123, "Hello");

            // Assert
            result.Should().Contain("order_123.mp3");
        }

        [Fact]
        public async Task GetOrGenerateScanAudio_FileExists_ReturnsUrl()
        {
            // Arrange
            _fakeHandler.Handler = req => new HttpResponseMessage(HttpStatusCode.OK);

            // Act
            var result = await _sut.GetOrGenerateScanAudioAsync(1, "Text");

            // Assert
            result.Should().Contain("scan_1.mp3");
        }

        [Fact]
        public async Task GetOrGeneratePaymentReceivedAudio_FileExists_ReturnsUrl()
        {
            // Arrange
            _fakeHandler.Handler = req => new HttpResponseMessage(HttpStatusCode.OK);

            // Act
            var result = await _sut.GetOrGeneratePaymentReceivedAudioAsync(123, 50000);

            // Assert
            result.Should().Contain("payment.mp3");
        }

        [Fact]
        public async Task GetOrGenerateScanAudio_FileExists_DoesNotCallTtsOrUpload()
        {
            // Arrange
            // Nếu file đã tồn tại (HEAD 200) thì không được gọi OpenAI (POST speech) hay upload VPS (POST upload)
            _fakeHandler.Handler = req =>
            {
                if (req.Method == HttpMethod.Head) return new HttpResponseMessage(HttpStatusCode.OK);
                throw new InvalidOperationException("Không được gọi TTS/Upload khi file đã tồn tại.");
            };

            // Act
            var result = await _sut.GetOrGenerateScanAudioAsync(7, "Xin chào");

            // Assert
            result.Should().Contain("scan_7.mp3");
        }

        [Fact]
        public async Task GetOrGeneratePaymentReceivedAudio_FileExists_DoesNotCallTtsOrUpload()
        {
            // Arrange
            _fakeHandler.Handler = req =>
            {
                if (req.Method == HttpMethod.Head) return new HttpResponseMessage(HttpStatusCode.OK);
                throw new InvalidOperationException("Không được gọi TTS/Upload khi file đã tồn tại.");
            };

            // Act
            var result = await _sut.GetOrGeneratePaymentReceivedAudioAsync(9, 12345);

            // Assert
            result.Should().Contain("payment.mp3");
        }

        [Fact]
        public async Task GetOrGeneratePaymentReceivedAudio_FileNotExists_GeneratesAndUploads()
        {
            // Arrange
            _fakeHandler.Handler = req =>
            {
                // 1) CheckFileExistsAsync
                if (req.Method == HttpMethod.Head) return new HttpResponseMessage(HttpStatusCode.NotFound);

                // 2) GenerateTtsAudioFromOpenAI
                if (req.Method == HttpMethod.Post && req.RequestUri != null &&
                    req.RequestUri.ToString().StartsWith("http://openai.com/speech", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
                    };
                }

                // 3) UploadAudioToVpsAsync
                if (req.Method == HttpMethod.Post && req.RequestUri != null &&
                    req.RequestUri.ToString().StartsWith("http://vps.com/upload", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK);
                }

                return new HttpResponseMessage(HttpStatusCode.OK);
            };

            // Act
            var result = await _sut.GetOrGeneratePaymentReceivedAudioAsync(77, 50000);

            // Assert
            result.Should().Contain("payment.mp3");
        }

        [Fact]
        public async Task UploadAudioToVps_WhenUploadFails_ThrowsException()
        {
            // Arrange
            _fakeHandler.Handler = req =>
            {
                if (req.Method == HttpMethod.Head) return new HttpResponseMessage(HttpStatusCode.NotFound);

                if (req.Method == HttpMethod.Post && req.RequestUri != null &&
                    req.RequestUri.ToString().StartsWith("http://openai.com/speech", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
                    };
                }

                if (req.Method == HttpMethod.Post && req.RequestUri != null &&
                    req.RequestUri.ToString().StartsWith("http://vps.com/upload", StringComparison.OrdinalIgnoreCase))
                {
                    return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                }

                return new HttpResponseMessage(HttpStatusCode.OK);
            };

            // Act
            Func<Task> act = () => _sut.GetOrGenerateOrderAudioAsync(1, "Text");

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("*Lỗi upload lên VPS*InternalServerError*");
        }

        #endregion

        #region 3. Private Method Indirect Coverage

        [Fact]
        public async Task CheckFileExists_HttpException_ReturnsFalse()
        {
            // Arrange
            _fakeHandler.Handler = req =>
            {
                if (req.Method == HttpMethod.Head) throw new HttpRequestException();
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1, 2, 3 }) };
            };

            // Act
            var result = await _sut.GetOrGenerateOrderAudioAsync(1, "Text");

            // Assert
            result.Should().NotBeNull();
        }

        [Fact]
        public async Task GenerateTts_OpenAiFails_ThrowsException()
        {
            // Arrange
            _fakeHandler.Handler = req => {
                if (req.Method == HttpMethod.Head) return new HttpResponseMessage(HttpStatusCode.NotFound);
                return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("OpenAI Error") };
            };

            // Act
            Func<Task> act = () => _sut.GetOrGenerateOrderAudioAsync(1, "Text");

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("*Lỗi gọi API OpenAI*");
        }

        [Fact]
        public async Task UploadFromBytes_Valid_ReturnsUrl()
        {
            _mockStorageService.Setup(s => s.GetPublicUrl(It.IsAny<string>(), It.IsAny<string>())).Returns("http://pub.url");
            var result = await _sut.UploadFromBytesAsync(new byte[] { 1 }, "test.png");
            result.Should().Be("http://pub.url");
        }

        [Fact]
        public async Task GetOrGenerateOrderAudio_FileExists_ReturnsUrl()
        {
            _fakeHandler.Handler = req => new HttpResponseMessage(HttpStatusCode.OK);
            var result = await _sut.GetOrGenerateOrderAudioAsync(1, "Hello");
            result.Should().Contain("order_1.mp3");
        }
        #endregion

        #region 4. Supabase Specific Methods

        [Fact]
        public async Task UploadOrderQr_Success_ReturnsUrl()
        {
            // Arrange
            _mockStorageService.Setup(s => s.GetPublicUrl(It.IsAny<string>(), It.IsAny<string>())).Returns("http://qr.url");

            // Act
            var result = await _sut.UploadOrderQrAsync(new byte[] { 1 }, Guid.NewGuid());

            // Assert
            result.Should().Be("http://qr.url");
        }

        [Fact]
        public void GetOrderQrUrl_ReturnsPublicUrl()
        {
            // Arrange
            _mockStorageService.Setup(s => s.GetPublicUrl(It.IsAny<string>(), It.IsAny<string>())).Returns("http://qr.url");

            // Act
            var result = _sut.GetOrderQrUrl(Guid.NewGuid());

            // Assert
            result.Should().Be("http://qr.url");
        }

        #endregion

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
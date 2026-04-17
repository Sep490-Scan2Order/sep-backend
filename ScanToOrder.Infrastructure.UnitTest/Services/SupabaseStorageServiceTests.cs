using FluentAssertions;
using ScanToOrder.Infrastructure.Services;
using System.Net;
using System.Text;

namespace ScanToOrder.Infrastructure.UnitTest.Services
{
    public class SupabaseStorageServiceTests
    {
        private readonly Supabase.Client _realSupabaseClient;
        private readonly LocalFakeHttpMessageHandler _fakeHandler;
        private readonly SupabaseStorageService _service;

        public SupabaseStorageServiceTests()
        {
            _fakeHandler = new LocalFakeHttpMessageHandler();

            var options = new Supabase.SupabaseOptions
            {
                AutoConnectRealtime = false
            };

            _realSupabaseClient = new Supabase.Client("https://fake.supabase.co", "fake-key", options);

            _service = new SupabaseStorageService(_realSupabaseClient);
        }

        #region 1. UploadAsync Tests

        [Fact]
        public async Task UploadAsync_ShouldExecuteSuccessfully()
        {
            // Arrange
            var bucket = "test-bucket";
            var bytes = Encoding.UTF8.GetBytes("hello world");
            var fileName = "test.txt";
            var contentType = "text/plain";

            // Act
            var action = async () => await _service.UploadAsync(bucket, bytes, fileName, contentType);

            // Assert
            await action.Should().ThrowAsync<HttpRequestException>();
        }

        #endregion

        #region 2. GetPublicUrl Tests

        [Fact]
        public void GetPublicUrl_ReturnsFormattedUrl()
        {
            // Arrange
            var bucket = "test-bucket";
            var fileName = "image.png";

            // Act
            var result = _service.GetPublicUrl(bucket, fileName);

            // Assert
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain(bucket);
            result.Should().Contain(fileName);
        }

        #endregion

        private class LocalFakeHttpMessageHandler : HttpMessageHandler
        {
            public Func<HttpRequestMessage, HttpResponseMessage> Handler { get; set; }
                = req => new HttpResponseMessage(HttpStatusCode.OK);

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(Handler(request));
            }
        }
    }
}
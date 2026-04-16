using FluentAssertions;
using Moq;
using OpenAI;
using OpenAI.Embeddings; // Chứa OpenAIEmbedding
using ScanToOrder.Infrastructure.Services;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Reflection; // Đôi khi cần cho PipelineResponse

namespace ScanToOrder.Infrastructure.UnitTest.Services
{
    public class OpenAiServiceTests
    {
        private readonly Mock<OpenAIClient> _mockClient;
        private readonly Mock<EmbeddingClient> _mockEmbeddingClient;
        private readonly OpenAiService _service;

        public OpenAiServiceTests()
        {
            _mockClient = new Mock<OpenAIClient>();
            _mockEmbeddingClient = new Mock<EmbeddingClient>();

            // Setup chuỗi gọi hàm: Client -> EmbeddingClient
            _mockClient
                .Setup(x => x.GetEmbeddingClient(It.IsAny<string>()))
                .Returns(_mockEmbeddingClient.Object);

            _service = new OpenAiService(_mockClient.Object);
        }
        private OpenAIEmbedding CreateMockEmbedding(float[] values)
        {
            var memory = new ReadOnlyMemory<float>(values);

            // Tìm constructor: (ReadOnlyMemory<float> embedding, int index)
            // Lưu ý: SDK v2.10 thường dùng thứ tự này
            var constructor = typeof(OpenAIEmbedding).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .FirstOrDefault(c => c.GetParameters().Any(p => p.ParameterType == typeof(ReadOnlyMemory<float>)));

            if (constructor == null) throw new Exception("Constructor not found");

            var parameters = constructor.GetParameters();
            object[] args = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType == typeof(ReadOnlyMemory<float>)) args[i] = memory;
                else if (parameters[i].ParameterType == typeof(int)) args[i] = 0;
                else args[i] = null!; // Cho các tham số khác nếu có
            }

            return (OpenAIEmbedding)constructor.Invoke(args);
        }

        [Fact]
        public async Task GetEmbeddingAsync_ShouldReturnFloats_WhenSuccessful()
        {
            // Arrange
            var text = "sample text";
            var expectedFloats = new float[] { 0.1f, 0.2f, 0.3f };
            var embedding = CreateMockEmbedding(expectedFloats);

            // 1. Mock PipelineResponse để tránh ArgumentNullException
            var mockResponse = new Mock<PipelineResponse>();

            // 2. Tạo ClientResult với mockResponse thay vì null
            var clientResult = (ClientResult<OpenAIEmbedding>)ClientResult.FromValue(embedding, mockResponse.Object);

            _mockEmbeddingClient
                .Setup(x => x.GenerateEmbeddingAsync(
                    It.IsAny<string>(),
                    It.IsAny<EmbeddingGenerationOptions>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(clientResult));

            // Act
            var result = await _service.GetEmbeddingAsync(text);

            // Assert
            result.Should().BeEquivalentTo(expectedFloats);
        }

        [Fact]
        public async Task GetEmbeddingAsync_ShouldThrow_WhenClientFails()
        {
            // Arrange
            _mockEmbeddingClient
                .Setup(x => x.GenerateEmbeddingAsync(
                    It.IsAny<string>(),
                    It.IsAny<EmbeddingGenerationOptions>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("OpenAI Error"));

            // Act
            var act = () => _service.GetEmbeddingAsync("test");

            // Assert
            await act.Should().ThrowAsync<Exception>().WithMessage("OpenAI Error");
        }

    }
}
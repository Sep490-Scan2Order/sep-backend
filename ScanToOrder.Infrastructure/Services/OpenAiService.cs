using OpenAI;
using ScanToOrder.Application.Interfaces;

namespace ScanToOrder.Infrastructure.Services;

public class OpenAiService : IOpenAiService
{
    private readonly OpenAIClient _client;

    public OpenAiService(OpenAIClient client)
    {
        _client = client;
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        var embeddingClient = _client.GetEmbeddingClient("text-embedding-3-small");
        var response = await embeddingClient.GenerateEmbeddingAsync(text);
        return response.Value.ToFloats().ToArray();
    }
}
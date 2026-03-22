namespace ScanToOrder.Application.Interfaces;

public interface IOpenAiService
{
    Task<float[]> GetEmbeddingAsync(string text);
}
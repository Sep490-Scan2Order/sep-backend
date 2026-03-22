namespace ScanToOrder.Infrastructure.Configuration;


public class AiSettings
{
    public string GeminiKey { get; set; } = string.Empty;
    public string GeminiModel { get; set; } = string.Empty;   
    public string? HuggingFaceApiKey { get; set; }
}

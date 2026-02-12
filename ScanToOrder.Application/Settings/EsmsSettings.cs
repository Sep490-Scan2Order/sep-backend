using System.Text.Json.Serialization;

namespace ScanToOrder.Application.Settings;

public class EsmsSettings
{
    [JsonPropertyName("ApiKey")]
    public string ApiKey { get; set; } = string.Empty;

    [JsonPropertyName("SecretKey")]
    public string SecretKey { get; set; } = string.Empty;

    [JsonPropertyName("Brandname")]
    public string Brandname { get; set; } = string.Empty;

    [JsonPropertyName("SmsType")]
    public string SmsType { get; set; } = "2";

    [JsonPropertyName("IsUnicode")]
    public string IsUnicode { get; set; } = "0";

    [JsonPropertyName("campaignid")]
    public string CampaignId { get; set; } = string.Empty;

    [JsonPropertyName("CallbackUrl")]
    public string CallbackUrl { get; set; } = string.Empty;
}

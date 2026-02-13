namespace ScanToOrder.Infrastructure.Configuration
{
    public class JwtSettings
    {
        public string AccessSecretKey { get; set; } = string.Empty;
        public string RefreshSecretKey { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public int AccessExpiration { get; set; }
        public int RefreshExpiration { get; set; }
    }
}

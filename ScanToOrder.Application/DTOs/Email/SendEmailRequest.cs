namespace ScanToOrder.Application.DTOs.Email
{
    public class SendEmailRequest
    {
        public string To { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string HtmlContent { get; set; } = string.Empty;
    }
}

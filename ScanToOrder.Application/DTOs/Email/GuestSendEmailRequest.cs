namespace ScanToOrder.Application.DTOs.Email
{
    public class GuestSendEmailRequest
    {
        public string From { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string HtmlContent { get; set; } = string.Empty;
    }
}

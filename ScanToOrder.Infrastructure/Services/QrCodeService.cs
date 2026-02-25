using Microsoft.Extensions.Configuration;
using QRCoder;
using ScanToOrder.Application.Interfaces;

namespace ScanToOrder.Infrastructure.Services
{
    public class QrCodeService : IQrCodeService
    {
        private readonly IConfiguration _configuration;
        public QrCodeService(IConfiguration configuration) 
        { 
            _configuration = configuration;
        }
        public byte[] GenerateRestaurantQrCodeBytes(string slug)
        {
            string baseUrl = _configuration["FrontEndUrl:scan2order_id_vn"]!;

            string fullUrl = $"{baseUrl.TrimEnd('/')}/{slug}";

            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(fullUrl, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);

            return qrCode.GetGraphic(20);
        }
    }
}

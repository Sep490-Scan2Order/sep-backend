using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.Interfaces;

namespace ScanToOrder.Api.Controllers
{
    public class QrCodeController : BaseController
    {
        private readonly IQrCodeService _qrCodeService;

        public QrCodeController(IQrCodeService qrCodeService)
        {
            _qrCodeService = qrCodeService;
        }

        [HttpGet("view-restaurant-qr")]
        public IActionResult ViewRestaurantQr(string restaurantId)
        {
            var imageBytes = _qrCodeService.GenerateRestaurantQrCodeBytes(restaurantId);
            return File(imageBytes, "image/png");
        }
    }
}

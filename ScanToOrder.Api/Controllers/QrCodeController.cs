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
        public IActionResult ViewRestaurantQr(string restaurantSlug)
        {
            var imageBytes = _qrCodeService.GenerateRestaurantQrCodeBytes(restaurantSlug);
            return File(imageBytes, "image/png");
        }
    }
}

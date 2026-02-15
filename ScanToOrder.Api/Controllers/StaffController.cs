using Microsoft.AspNetCore.Mvc;

namespace ScanToOrder.Api.Controllers
{
    public class StaffController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}

using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Infrastructure.Services;
using ScanToOrder.Application.Wrapper;
using System.Threading.Tasks;

namespace ScanToOrder.Api.Controllers
{
    [ApiController]
    [Route("api/dev")]
    [AllowAnonymous] // Only for development/testing
    public class DevController : BaseController
    {
        private readonly IBackgroundJobClient _backgroundJobClient;

        public DevController(IBackgroundJobClient backgroundJobClient)
        {
            _backgroundJobClient = backgroundJobClient;
        }

        [HttpPost("seed-orders/{restaurantId}")]
        public IActionResult SeedOrders(int restaurantId, [FromQuery] int numberOfOrders = 1000)
        {
            _backgroundJobClient.Enqueue<OrderDataSeederJob>(x => x.ExecuteAsync(restaurantId, numberOfOrders));
            
            return Ok(ApiResponse<string>.Success($"Đã đưa tiến trình tạo {numberOfOrders} đơn hàng mẫu cho nhà hàng {restaurantId} vào hàng đợi Hangfire."));
        }

        [HttpPost("train-ai")]
        public IActionResult TrainAI()
        {
            _backgroundJobClient.Enqueue<AITrainingJob>(x => x.ExecuteAsync());
            
            return Ok(ApiResponse<string>.Success("Đã đưa tiến trình huấn luyện AI (Matrix Factorization) vào hàng đợi Hangfire."));
        }

        [HttpDelete("clear-seeded-data/{restaurantId}")]
        public IActionResult ClearSeededData(int restaurantId)
        {
            _backgroundJobClient.Enqueue<OrderDataClearanceJob>(x => x.ExecuteAsync(restaurantId));
            
            return Ok(ApiResponse<string>.Success($"Đã đưa tiến trình dọn dẹp toàn bộ dữ liệu mẫu (Seeder) của nhà hàng {restaurantId} vào hàng đợi Hangfire."));
        }
    }
}

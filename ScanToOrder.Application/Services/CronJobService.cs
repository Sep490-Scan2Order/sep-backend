using Microsoft.Extensions.Logging;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services;

public class CronJobService : ICronJobService
{
        private readonly ILogger<CronJobService> _logger;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrderService _orderService;

        public CronJobService(ILogger<CronJobService> logger, IUnitOfWork unitOfWork, IOrderService orderService)
        {
            _logger = logger;
            _unitOfWork = unitOfWork;
            _orderService = orderService;
        }
        
        public async Task CancelExpiredUnpaidOrdersAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Bắt đầu chạy CronJob: CancelExpiredUnpaidOrdersAsync vào lúc {Time}", DateTimeOffset.Now);
            
            try
            {
                await _orderService.CancelExpiredUnpaidOrdersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi chạy CronJob: CancelExpiredUnpaidOrdersAsync");
            }
            
            _logger.LogInformation("Đã hoàn thành CronJob: CancelExpiredUnpaidOrdersAsync");
        }
    }
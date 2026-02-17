using ScanToOrder.Application.DTOs.Notification;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Entities.Notifications;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services
{
    public class NotifyTenantService : INotifyTenantService
    {
        private readonly IUnitOfWork _unitOfWork;
        public NotifyTenantService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public async Task<ApiResponse<CreateNotifyTenantDtoResponse>> CreateNotifyTenantAsync(CreateNotifyTenantDtoRequest request)
        {
            var notifyTenant = new NotifyTenant
            {
                NotificationId = request.NotificationId,
                TenantId = request.TenantId
            };
            await _unitOfWork.NotifyTenants.AddAsync(notifyTenant);
            await _unitOfWork.SaveAsync();
            return new ApiResponse<CreateNotifyTenantDtoResponse>
            {
                IsSuccess = true,
                Data = new CreateNotifyTenantDtoResponse
                {
                    Id = notifyTenant.Id,
                    NotificationId = notifyTenant.NotificationId,
                    TenantId = notifyTenant.TenantId
                }
            };
        }

        public async Task<ApiResponse<IEnumerable<NotifyTenant>>> GetNotifyTenantsAsync()
        {
            var notifyTenants = await _unitOfWork.NotifyTenants.GetAllAsync();
            return new ApiResponse<IEnumerable<NotifyTenant>>
            {
                IsSuccess = true,
                Data = notifyTenants
            };
        }
    }
}

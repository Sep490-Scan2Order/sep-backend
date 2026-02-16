using ScanToOrder.Application.DTOs.Notification;
using ScanToOrder.Application.Interfaces;
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
        public async Task<CreateNotifyTenantDtoResponse> CreateNotifyTenantAsync(CreateNotifyTenantDtoRequest request)
        {
            var notifyTenant = new NotifyTenant
            {
                NotificationId = request.NotificationId,
                TenantId = request.TenantId
            };
            await _unitOfWork.NotifyTenants.AddAsync(notifyTenant);
            await _unitOfWork.SaveAsync();
            return new CreateNotifyTenantDtoResponse
            {
                Id = notifyTenant.NotifyTenantId,
                NotificationId = notifyTenant.NotificationId,
                TenantId = notifyTenant.TenantId
            };
        }
    }
}

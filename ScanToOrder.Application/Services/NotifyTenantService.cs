using ScanToOrder.Application.DTOs.Notification;
using ScanToOrder.Application.DTOs.NotifyTenant;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Entities.Notifications;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
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
        public async Task<List<CreateNotifyTenantDtoResponse>> CreateNotifyTenantAsync(CreateNotifyTenantDtoRequest request)
        {
            var notifyTenants = request.TenantIds.Select(tenantId => new NotifyTenant
            {
                NotificationId = request.NotificationId,
                TenantId = tenantId
            }).ToList();
            await _unitOfWork.NotifyTenants.AddRangeAsync(notifyTenants);
            await _unitOfWork.SaveAsync();

            return notifyTenants.Select(nt => new CreateNotifyTenantDtoResponse
            {
                Id = nt.NotifyTenantId,
                NotificationId = nt.NotificationId,
                TenantId = nt.TenantId,
                Status = nt.Status
            }).ToList();
        }

        public async Task<IEnumerable<NotifyTenant>> GetNotifyTenantsAsync()
        {
            var notifyTenants = await _unitOfWork.NotifyTenants.GetAllAsync();
            return notifyTenants;
        }

        public async Task<int> CountTotalNotifyByTenantId(Guid tenantId, NotifyTenantStatus? status = null)
        {
            if (status.HasValue)
            {
                return await _unitOfWork.NotifyTenants.CountAsync(nt => nt.TenantId == tenantId && nt.Status == status.Value);
            }
            else
            {
                return await _unitOfWork.NotifyTenants.CountAsync(nt => nt.TenantId == tenantId);
            }
        }

        public async Task<string> UpdateStatusToReadAsync(Guid tenantId, UpdateNotifyTenantStatusRequestDto request)
        {
            var entities = await _unitOfWork.NotifyTenants.FindAsync(nt =>
                nt.TenantId == tenantId &&
                request.NotificationIds.Contains(nt.NotificationId));

            var listToUpdate = entities.ToList();

            if (!listToUpdate.Any())
            {
                throw new DomainException(NotifyTenantMessage.NotifyTenantError.NOTIFY_TENANT_NOT_FOUND);
            }

            foreach (var entity in listToUpdate)
            {
                entity.Status = request.Status;
                entity.ReadAt = request.ReadAt;
                _unitOfWork.NotifyTenants.Update(entity);
            }

            await _unitOfWork.SaveAsync();

            return NotifyTenantMessage.NotifyTenantSuccess.ALL_NOTIFY_TENANT_READED;
        }

        public async Task<List<NotifyDetailDtoResponse>> GetNotifiTenantDetailsAsync(Guid tenantId)
        {
            var notifyTenants = await _unitOfWork.NotifyTenants.GetDetailsByTenantIdAsync(tenantId);

            return notifyTenants.Select(nt => new NotifyDetailDtoResponse
            {
                NotificationId = nt.NotificationId,
                NotifyTitle = nt.Notification.NotifyTitle,
                NotifySub = nt.Notification.NotifySub,
                SystemBlogUrl = nt.Notification.SystemBlogUrl,
                Status = nt.Status,
                SentAt = nt.Notification.SentAt,
                ReadAt = nt.ReadAt
            }).ToList();
        }

        public async Task<(List<NotifyDetailDtoResponse> Items, int TotalCount)> GetNotifyDetailsByTenantIdSortBySentAtAsync(int pageIndex, int pageSize, Guid tenantId)
        {
            var (items, totalCount) = await _unitOfWork.NotifyTenants.GetNotifyDetailsByTenantIdSortBySentAtAsync(pageIndex, pageSize, tenantId);
            return (items.Select(nt => new NotifyDetailDtoResponse
            {
                NotificationId = nt.NotificationId,
                NotifyTitle = nt.Notification.NotifyTitle,
                NotifySub = nt.Notification.NotifySub,
                SystemBlogUrl = nt.Notification.SystemBlogUrl,
                Status = nt.Status,
                SentAt = nt.Notification.SentAt,
                ReadAt = nt.ReadAt
            }).ToList(), totalCount);
        }
    }
}

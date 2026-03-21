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
        private readonly IRealtimeService _realtimeService;
        private readonly IEmailService _emailService;
        public NotifyTenantService(IUnitOfWork unitOfWork, IRealtimeService realtimeService, IEmailService emailService)
        {
            _unitOfWork = unitOfWork;
            _realtimeService = realtimeService;
            _emailService = emailService;
        }
        public async Task<List<CreateNotifyTenantDtoResponse>> CreateNotifyTenantAsync(CreateNotifyTenantDtoRequest request)
        {
            var notification = await _unitOfWork.Notifications.GetByIdAsync(request.NotificationId);
            var notifyTenants = request.TenantIds.Select(tenantId => new NotifyTenant
            {
                NotificationId = request.NotificationId,
                TenantId = tenantId
            }).ToList();
            await _unitOfWork.NotifyTenants.AddRangeAsync(notifyTenants);
            await _unitOfWork.SaveAsync();

            foreach (var tenantId in request.TenantIds)
            {
                await _realtimeService.SendNotificationToTenant(tenantId.ToString(), new
                {
                    Message = RealtimeMessage.RealtimeSuccess.YOU_HAVE_NEW_NOTIFICATION,
                    request.NotificationId,
                    Url = notification!.SystemBlogUrl
                });

                var currentTenant = await _unitOfWork.Tenants.GetByFieldsIncludeAsync(
                    t => t.AccountId == tenantId || t.Id == tenantId,
                        t => t.Account);

                if (currentTenant?.Account?.Email != null)
                {
                    try
                    {
                        string htmlContent = $@"
        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e0e0e0; border-radius: 8px; overflow: hidden;'>
            <div style='background-color: #2D3E50; padding: 20px; text-align: center;'>
                <h1 style='color: #ffffff; margin: 0; font-size: 24px;'>Scan2Order</h1>
            </div>
            
            <div style='padding: 30px; line-height: 1.6; color: #333333;'>
                <h2 style='color: #2D3E50;'>Chào {currentTenant.Name ?? "Đối tác"},</h2>
                <p>Bạn vừa nhận được một thông báo mới quan trọng từ hệ thống quản lý <b>Scan2Order</b>.</p>
                
                <div style='background-color: #f9f9f9; border-left: 4px solid #4CAF50; padding: 15px; margin: 20px 0;'>
                    <p style='margin: 0; font-weight: bold; color: #555;'>{notification.NotifyTitle}</p>
                    <p style='margin: 5px 0 0; font-size: 0.9em; color: #777;'>{notification.NotifySub}</p>
                </div>

                <p>Vui lòng nhấn vào nút bên dưới để xem chi tiết nội dung và thực hiện các thao tác cần thiết:</p>
                
                <div style='text-align: center; margin: 30px 0;'>
                    <a href='{notification.SystemBlogUrl}' 
                       style='background-color: #4CAF50; color: white; padding: 12px 25px; text-decoration: none; font-weight: bold; border-radius: 4px; display: inline-block;'>
                       XEM CHI TIẾT THÔNG BÁO
                    </a>
                </div>

                <p style='font-size: 0.85em; color: #888;'>Nếu nút trên không hoạt động, bạn có thể sao chép liên kết này vào trình duyệt:<br/>
                <a href='{notification.SystemBlogUrl}' style='color: #4CAF50;'>{notification.SystemBlogUrl}</a></p>
            </div>

            <div style='background-color: #f4f4f4; padding: 15px; text-align: center; font-size: 12px; color: #999;'>
                <p>© 2026 Scan2Order Team. All rights reserved.<br/>
                Đây là email tự động, vui lòng không trả lời email này.</p>
            </div>
        </div>";

                        await _emailService.SendEmailViaIdDomainAsync(
                            to: currentTenant.Account.Email,
                            subject: $"[Scan2Order] {notification.NotifyTitle}",
                            htmlContent: htmlContent);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Email Error: {ex.Message}");
                    }
                }

                var unreadCount = await CountTotalNotifyByTenantId(tenantId, NotifyTenantStatus.Unread);

                await _realtimeService.NotifyCountChanged(tenantId.ToString(), unreadCount);

                await _realtimeService.NotifyListChanged(tenantId.ToString());
            }

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
            int count = status.HasValue
                ? await _unitOfWork.NotifyTenants.CountAsync(nt => nt.TenantId == tenantId && nt.Status == status.Value)
                : await _unitOfWork.NotifyTenants.CountAsync(nt => nt.TenantId == tenantId);
            return count;
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

            var unreadCount = await CountTotalNotifyByTenantId(tenantId, NotifyTenantStatus.Unread);
            await _realtimeService.NotifyCountChanged(tenantId.ToString(), unreadCount);

            await _realtimeService.NotifyListChanged(tenantId.ToString());

            return NotifyTenantMessage.NotifyTenantSuccess.ALL_NOTIFY_TENANT_READED;
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

using ScanToOrder.Domain.Entities.Blogs;
using ScanToOrder.Domain.Entities.Configuration;
using ScanToOrder.Domain.Entities.Notifications;
using ScanToOrder.Domain.Entities.Points;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Entities.Vouchers;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IAuthenticationUserRepository AuthenticationUsers { get; }
        IGenericRepository<Tenant> Tenants { get; }
        IGenericRepository<Staff> Staffs { get; }
        IGenericRepository<Restaurant> Restaurants { get; }
        IGenericRepository<Customer> Customers { get; }
        IGenericRepository<PointHistory> PointHistories { get; }
        IMemberPointRepository MemberPoints { get; }
        IGenericRepository<Plan> Plans { get; }
        IGenericRepository<Configurations> Configurations { get; }
        IGenericRepository<SystemBlog> SystemBlogs { get; }
        IGenericRepository<NotifyTenant> NotifyTenants { get; }
        IGenericRepository<Notification> Notifications { get; }
        IGenericRepository<Voucher> Vouchers { get; }          
        IGenericRepository<MemberVoucher> MemberVouchers { get; }
        Task SaveAsync();
    }
}

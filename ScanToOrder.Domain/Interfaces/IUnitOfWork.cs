using ScanToOrder.Domain.Entities.Blogs;
using ScanToOrder.Domain.Entities.Configuration;
using ScanToOrder.Domain.Entities.Notifications;
using ScanToOrder.Domain.Entities.Points;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Entities.Vouchers;
using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Entities.Menu;
using ScanToOrder.Domain.Entities.Promotions;
using ScanToOrder.Domain.Entities.CashReport;
using ScanToOrder.Domain.Entities.Wallet;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IAuthenticationUserRepository AuthenticationUsers { get; }
        ITenantRepository Tenants { get; }
        IStaffRepository Staffs { get; }
        IRestaurantRepository Restaurants { get; }
        ICustomerRepository Customers { get; }
        IPointHistoryRepository PointHistories { get; }
        IMemberPointRepository MemberPoints { get; }
        IPlanRepository Plans { get; }
        IConfigurationRepository Configurations { get; }
        ISystemBlogRepository SystemBlogs { get; }
        INotifyTenantRepository NotifyTenants { get; }
        INotificationRepository Notifications { get; }
        IVoucherRepository Vouchers { get; }          
        IMemberVoucherRepository MemberVouchers { get; }
        IOrderRepository Orders { get; }
        ITransactionRepository Transactions { get; }
        IOrderDetailRepository OrderDetails { get; }
        IDishesRepository Dishes { get; }
        ICategoryRepository Categories { get; }
        IBranchDishConfigRepository BranchDishConfigs { get; }
        IMenuRestaurantRepository MenuRestaurants { get; }
        IMenuTemplateRepository MenuTemplates { get; }
        IPromotionRepository Promotions { get; }
        IPromotionDishRepository PromotionDishes { get; }
        IRestaurantPromotionRepository RestaurantPromotions { get; }
        IAddOnRepository AddOns { get; }
        ISubscriptionRepository Subscriptions { get; }
        IAdminWalletRepository AdminWallets { get; }
        ITenantWalletRepository TenantWallets { get; }
        IWalletTransactionRepository WalletTransactions { get; }
        ICashDrawerReportRepository CashDrawerReports { get; }
        IBankRepository Banks { get; }
        Task<IDbTransaction> BeginTransactionAsync();
        Task SaveAsync();
    }
}

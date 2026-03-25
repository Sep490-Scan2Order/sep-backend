using ScanToOrder.Domain.Entities.Blogs;
using ScanToOrder.Domain.Entities.Configuration;
using ScanToOrder.Domain.Entities.Notifications;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Entities.Menu;
using ScanToOrder.Domain.Entities.Promotions;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Domain.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        IAuthenticationUserRepository AuthenticationUsers { get; }
        ITenantRepository Tenants { get; }
        IStaffRepository Staffs { get; }
        IRestaurantRepository Restaurants { get; }
        IPlanRepository Plans { get; }
        IConfigurationRepository Configurations { get; }
        ISystemBlogRepository SystemBlogs { get; }
        INotifyTenantRepository NotifyTenants { get; }
        INotificationRepository Notifications { get; }
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
        ISubscriptionRepository Subscriptions { get; }
        IBankRepository Banks { get; }
        IShiftRepository Shifts { get; }
        IShiftReportRepository ShiftReports { get; }
        IPaymentTransactionRepository PaymentTransactions { get; }
        ISubscriptionLogRepository SubscriptionLogs { get; }
        IComboDetailRepository ComboDetails { get; }
        Task<IDbTransaction> BeginTransactionAsync();
        Task SaveAsync();
    }
}

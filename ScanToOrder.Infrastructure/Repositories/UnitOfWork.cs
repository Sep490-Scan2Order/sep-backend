using ScanToOrder.Domain.Interfaces;
using ScanToOrder.Infrastructure.Context;

namespace ScanToOrder.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _context;

        public IAuthenticationUserRepository AuthenticationUsers { get; }
        public ITenantRepository Tenants { get; }
        public IStaffRepository Staffs { get; }
        public IRestaurantRepository Restaurants { get; }
        public IConfigurationRepository Configurations { get; }
        public ISystemBlogRepository SystemBlogs { get; }
        public INotifyTenantRepository NotifyTenants { get; }
        public INotificationRepository Notifications { get; }
        public IPlanRepository Plans { get; }
        public IOrderRepository Orders { get; }
        public ITransactionRepository Transactions { get; }
        public IOrderDetailRepository OrderDetails { get; }
        public IDishesRepository Dishes { get; }
        public ICategoryRepository Categories { get; }
        public IBranchDishConfigRepository BranchDishConfigs { get; }
        public IMenuRestaurantRepository MenuRestaurants { get; }
        public IMenuTemplateRepository MenuTemplates { get; }
        public IPromotionRepository Promotions { get; }
        public IPromotionDishRepository PromotionDishes { get; }
        public IRestaurantPromotionRepository RestaurantPromotions { get; }
        public ISubscriptionRepository Subscriptions { get; }
        public IBankRepository Banks { get; }
        public IShiftRepository Shifts { get; }
        public IShiftReportRepository ShiftReports { get; }
        public IPaymentTransactionRepository PaymentTransactions { get; }
        public ISubscriptionLogRepository SubscriptionLogs { get; }
        public IComboDetailRepository ComboDetails { get; }

        public UnitOfWork(
            AppDbContext context,
            IAuthenticationUserRepository authenticationUsers,
            ITenantRepository tenants,
            IStaffRepository staffs,
            IRestaurantRepository restaurants,
            IConfigurationRepository configurations,
            ISystemBlogRepository systemBlogs,
            INotifyTenantRepository notifyTenants,
            INotificationRepository notifications,
            IPlanRepository plans,
            IOrderRepository orders,
            ITransactionRepository transactions,
            IOrderDetailRepository orderDetails,
            IDishesRepository dishes,
            ICategoryRepository categories,
            IBranchDishConfigRepository branchDishConfigs,
            IMenuRestaurantRepository menuRestaurants,
            IMenuTemplateRepository menuTemplates,
            IPromotionRepository promotions,
            IPromotionDishRepository promotionDishes,
            IRestaurantPromotionRepository restaurantPromotions,
            ISubscriptionRepository subscriptions,
            IBankRepository banks,
            IShiftRepository shifts,
            IShiftReportRepository shiftReports,
            IPaymentTransactionRepository paymentTransactions,
            ISubscriptionLogRepository subscriptionLogs,
            IComboDetailRepository comboDetails)
        {
            _context = context;
            AuthenticationUsers = authenticationUsers;
            Tenants = tenants;
            Staffs = staffs;
            Restaurants = restaurants;
            Configurations = configurations;
            SystemBlogs = systemBlogs;
            NotifyTenants = notifyTenants;
            Notifications = notifications;
            Plans = plans;
            Orders = orders;
            Transactions = transactions;
            OrderDetails = orderDetails;
            Dishes = dishes;
            Categories = categories;
            BranchDishConfigs = branchDishConfigs;
            MenuRestaurants = menuRestaurants;
            MenuTemplates = menuTemplates;
            Promotions = promotions;
            PromotionDishes = promotionDishes;
            RestaurantPromotions = restaurantPromotions;
            Subscriptions = subscriptions;
            Banks = banks;
            Shifts = shifts;
            ShiftReports = shiftReports;
            PaymentTransactions = paymentTransactions;
            SubscriptionLogs = subscriptionLogs;
            ComboDetails = comboDetails;
        }

        public async Task<IDbTransaction> BeginTransactionAsync()
        {
            var tx = await _context.Database.BeginTransactionAsync();
            return new EfDbTransaction(tx);
        }

        public async Task SaveAsync()
        {
            await _context.SaveChangesAsync();
        }

        public void Dispose()
        {
            _context.Dispose();
        }
    }
}

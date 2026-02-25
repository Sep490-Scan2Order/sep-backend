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
        public ICustomerRepository Customers { get; }
        public IPointHistoryRepository PointHistories { get; }
        public IMemberPointRepository MemberPoints { get; }
        public IConfigurationRepository Configurations { get; }
        public ISystemBlogRepository SystemBlogs { get; }
        public INotifyTenantRepository NotifyTenants { get; }
        public INotificationRepository Notifications { get; }
        public IPlanRepository Plans { get; }
        public IVoucherRepository Vouchers { get; } 
        public IMemberVoucherRepository MemberVouchers { get; }
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
        public IAddOnRepository AddOns { get; }
        public ISubscriptionRepository Subscriptions { get; }
        public IAdminWalletRepository AdminWallets { get; }
        public ITenantWalletRepository TenantWallets { get; }
        public IWalletTransactionRepository WalletTransactions { get; }
        public ICashDrawerReportRepository CashDrawerReports { get; }
        public IBankRepository Banks { get; }

        public UnitOfWork(AppDbContext context, IBankRepository banks)
        {
            _context = context;
            Banks = banks;
            AuthenticationUsers = new AuthenticationUserRepository(_context);
            Tenants = new TenantRepository(_context);
            Staffs = new StaffRepository(_context);
            Restaurants = new RestaurantRepository(_context);
            Customers = new CustomerRepository(_context);
            PointHistories = new PointHistoryRepository(_context);
            MemberPoints = new MemberPointRepository(_context);
            Configurations = new ConfigurationRepository(_context);
            SystemBlogs = new SystemBlogRepository(_context);
            Plans = new PlanRepository(_context);
            NotifyTenants = new NotifyTenantRepository(_context);
            Notifications = new NotificationRepository(_context);
            Vouchers = new VoucherRepository(_context);
            MemberVouchers = new MemberVoucherRepository(_context);
            Orders = new OrderRepository(_context);
            Transactions = new TransactionRepository(_context);
            OrderDetails = new OrderDetailRepository(_context);
            Dishes = new DishesRepository(_context);
            Categories = new CategoryRepository(_context);
            BranchDishConfigs = new BranchDishConfigRepository(_context);
            MenuRestaurants = new MenuRestaurantRepository(_context);
            MenuTemplates = new MenuTemplateRepository(_context);
            Promotions = new PromotionRepository(_context);
            PromotionDishes = new PromotionDishRepository(_context);
            RestaurantPromotions = new RestaurantPromotionRepository(_context);
            AddOns = new AddOnRepository(_context);
            Subscriptions = new SubscriptionRepository(_context);
            AdminWallets = new AdminWalletRepository(_context);
            TenantWallets = new TenantWalletRepository(_context);
            WalletTransactions = new WalletTransactionRepository(_context);
            CashDrawerReports = new CashDrawerReportRepository(_context);
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

using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.Blogs;
using ScanToOrder.Domain.Entities.CashReport;
using ScanToOrder.Domain.Entities.Configuration;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Entities.Menu;
using ScanToOrder.Domain.Entities.Notifications;
using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Entities.Points;
using ScanToOrder.Domain.Entities.Promotions;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Entities.Vouchers;
using ScanToOrder.Domain.Entities.Wallet;
using ScanToOrder.Domain.Enums;
using System.Reflection;

namespace ScanToOrder.Infrastructure.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AuthenticationUser> AuthenticationUsers { get; set; } = null!;
    public DbSet<Customer> Customers { get; set; } = null!;
    public DbSet<MemberPoint> MemberPoints { get; set; } = null!;
    public DbSet<Restaurant> Restaurants { get; set; } = null!;
    public DbSet<Staff> Staffs { get; set; } = null!;
    public DbSet<Tenant> Tenants { get; set; } = null!;
    public DbSet<Plan> Plans { get; set; } = null!;
    public DbSet<AddOn> AddOns { get; set; } = null!;
    public DbSet<Subscription> Subscriptions { get; set; } = null!;
    public DbSet<AdminWallet> AdminWallet { get; set; } = null!;
    public DbSet<TenantWallet> TenantWallets { get; set; } = null!;
    public DbSet<WalletTransaction> WalletTransactions { get; set; } = null!;
    public DbSet<Dish> Dishes { get; set; } = null!;
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<OrderDetail> OrderDetails { get; set; } = null!;
    public DbSet<Promotion> Promotions { get; set; } = null!;
    public DbSet<Voucher> Vouchers { get; set; } = null!;
    public DbSet<MemberVoucher> MemberVouchers { get; set; } = null!;
    public DbSet<CashDrawerReport> CashDrawerReports { get; set; } = null!;
    public DbSet<MenuTemplate> MenuTemplates { get; set; } = null!;
    public DbSet<MenuRestaurant> MenuRestaurants { get; set; } = null!;
    public DbSet<RestaurantPromotion> RestaurantPromotions { get; set; } = null!;
    public DbSet<PointHistory> PointHistories { get; set; } = null!;
    public DbSet<Configurations> Configurations { get; set; } = null!;
    public DbSet<SystemBlog> SystemBlogs { get; set; } = null!;
    public DbSet<NotifyTenant> NotifyTenants { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.Entity<Restaurant>()
            .HasIndex(r => r.Location)
            .HasMethod("gist");

        modelBuilder.Entity<AuthenticationUser>()
            .Property(u => u.Role)
            .HasConversion<string>();

        modelBuilder.Entity<Voucher>()
            .Property(v => v.Status)
            .HasConversion<string>();

        modelBuilder.Entity<MemberPoint>()
        .HasOne(mp => mp.Customer)         
        .WithMany(c => c.MemberPoints)     
        .HasForeignKey(mp => mp.CustomerId) 
        .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PointHistory>()
            .ToTable("PointHistory");

        modelBuilder.Entity<PointHistory>()
            .HasOne(ph => ph.MemberPoint)
            .WithMany(mp => mp.PointHistories)
            .HasForeignKey(ph => ph.MemberPointId);

        modelBuilder.Entity<PointHistory>()
            .Property(ph => ph.Type)
            .HasConversion<string>();

        modelBuilder.Entity<PointHistory>()
            .HasOne(ph => ph.MemberVoucher)
            .WithOne(mv => mv.PointHistory)
            .HasForeignKey<PointHistory>(ph => ph.MemberVoucherId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Notification>()
            .Property(n => n.NotificationId)
            .UseIdentityByDefaultColumn();

        modelBuilder.Entity<Notification>()
            .Property(e => e.NotifyStatus)
            .HasConversion<string>(); 

        modelBuilder.Entity<Order>()
            .Property(o => o.Status)
            .HasConversion<string>();

        modelBuilder.Entity<Configurations>(entity =>
        {
            entity.HasNoKey(); 
        });

        modelBuilder.Entity<SystemBlog>()
        .Property(b => b.BlogType)
        .HasConversion<string>();

        modelBuilder.Entity<NotifyTenant>(entity =>
        {
            entity.ToTable("NotifyTenant");
            entity.HasKey(e => e.NotifyTenantId);

            entity.HasOne(d => d.Notification)
                  .WithMany(p => p.NotifyTenants)
                  .HasForeignKey(d => d.NotificationId)
                  .OnDelete(DeleteBehavior.Cascade); 

            entity.HasOne(d => d.Tenant)
                  .WithMany()
                  .HasForeignKey(d => d.TenantId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
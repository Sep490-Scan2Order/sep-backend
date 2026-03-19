using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.Bank;
using ScanToOrder.Domain.Entities.Blogs;
using ScanToOrder.Domain.Entities.Configuration;
using ScanToOrder.Domain.Entities.Dishes;
using ScanToOrder.Domain.Entities.Menu;
using ScanToOrder.Domain.Entities.Notifications;
using ScanToOrder.Domain.Entities.Orders;
using ScanToOrder.Domain.Entities.Promotions;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Entities.User;

using System.Reflection;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Entities.Shifts;

namespace ScanToOrder.Infrastructure.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AuthenticationUser> AuthenticationUsers { get; set; } = null!;
    public DbSet<Customer> Customers { get; set; } = null!;
    public DbSet<Restaurant> Restaurants { get; set; } = null!;
    public DbSet<Staff> Staffs { get; set; } = null!;
    public DbSet<Tenant> Tenants { get; set; } = null!;

    public DbSet<Plan> Plans { get; set; } = null!;
    public DbSet<Subscription> Subscriptions { get; set; } = null!;
    public DbSet<SubscriptionLog> SubscriptionLogs { get; set; } = null!;
    public DbSet<PaymentTransaction> PaymentTransactions { get; set; } = null!;

    public DbSet<Dish> Dishes { get; set; } = null!;
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<Transaction> Transactions { get; set; } = null!;
    public DbSet<OrderDetail> OrderDetails { get; set; } = null!;

    public DbSet<Promotion> Promotions { get; set; } = null!;
    public DbSet<RestaurantPromotion> RestaurantPromotions { get; set; } = null!;
    public DbSet<PromotionDish> PromotionDishes { get; set; } = null!;

    public DbSet<MenuTemplate> MenuTemplates { get; set; } = null!;
    public DbSet<MenuRestaurant> MenuRestaurants { get; set; } = null!;

    public DbSet<Shift> Shifts { get; set; } = null!;
    public DbSet<ShiftReport> ShiftReports { get; set; } = null!;

    public DbSet<Configurations> Configurations { get; set; } = null!;
    public DbSet<SystemBlog> SystemBlogs { get; set; } = null!;

    public DbSet<NotifyTenant> NotifyTenants { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;

    public DbSet<Banks> Banks { get; set; } = null!;
    public DbSet<Category> Categories { get; set; } = null!;

    public DbSet<BranchDishConfig> BranchDishConfigs { get; set; } = null!;
    public DbSet<ComboDetail> ComboDetails { get; set; } = null!;
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<Restaurant>()
            .HasIndex(r => r.Location)
            .HasMethod("gist");

        modelBuilder.Entity<Restaurant>()
            .HasIndex(r => r.TotalOrder)
            .IsDescending();

        modelBuilder.Entity<AuthenticationUser>()
            .Property(u => u.Role)
            .HasConversion<string>();


        modelBuilder.Entity<Notification>()
            .Property(n => n.NotificationId)
            .UseIdentityByDefaultColumn();

        modelBuilder.Entity<Notification>()
            .Property(e => e.NotifyStatus)
            .HasConversion<string>(); 

        modelBuilder.Entity<Order>()
            .Property(o => o.Status)
            .HasConversion<string>();

        modelBuilder.Entity<Order>()
            .Property(o => o.typeOrder)
            .HasConversion<string>();

        modelBuilder.Entity<Order>()
            .Property(o => o.RefundType)
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

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id);

           
            entity.Property(e => e.CategoryName)
                  .IsRequired()
                  .HasMaxLength(100);

            
            entity.HasOne(c => c.Tenant)
                  .WithMany(t => t.Category) 
                  .HasForeignKey(c => c.TenantId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<BranchDishConfig>()
                    .HasOne(b => b.Restaurant)
                    .WithMany(r => r.BranchDishConfigs)
                    .HasForeignKey(b => b.RestaurantId)
                    .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BranchDishConfig>()
                    .HasOne(b => b.Dish)
                    .WithMany(d => d.BranchDishConfigs)
                    .HasForeignKey(b => b.DishId)
                    .OnDelete(DeleteBehavior.Cascade);

        /*
        * Shift
           */
        modelBuilder.Entity<Shift>()
                   .Property(s => s.Status)
                .HasConversion<string>();
        
        modelBuilder.Entity<Shift>()
            .HasOne(s => s.Restaurants)
            .WithMany(r => r.Shifts)
            .HasForeignKey(s => s.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Shift>()
         .HasOne(s => s.Staffs)
         .WithMany(st => st.Shifts)
         .HasForeignKey(s => s.StaffId);

        /*
         * ShiftReport
         */
        modelBuilder.Entity<ShiftReport>()
            .HasOne(sr => sr.Shift)
            .WithMany()
            .HasForeignKey(sr => sr.ShiftId)
            .OnDelete(DeleteBehavior.Cascade);

        /*
         * PaymentTransaction
         */
        modelBuilder.Entity<PaymentTransaction>()
            .Property(p => p.Status)
            .HasConversion<string>();

        modelBuilder.Entity<PaymentTransaction>()
      .HasOne(p => p.Tenants)
      .WithMany(t => t.PaymentTransactions)
      .HasForeignKey(p => p.TenantId);

        /*
         * Subscription
         */
        modelBuilder.Entity<Subscription>()
            .Property(s => s.Status)
            .HasConversion<string>();

        modelBuilder.Entity<Subscription>()
            .HasOne(s => s.Restaurant)
            .WithOne(r => r.Subscription)
            .HasForeignKey<Subscription>(s => s.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        /*
         * Plan
         */
        modelBuilder.Entity<Plan>()
            .Property(p => p.Status)
            .HasConversion<string>();

        /*
         * SubscriptionLog
         */
        modelBuilder.Entity<SubscriptionLog>()
            .Property(s => s.ActionType)
            .HasConversion<string>();

        modelBuilder.Entity<SubscriptionLog>()
            .HasOne(s => s.Restaurants)
            .WithMany()
            .HasForeignKey(s => s.RestaurantId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SubscriptionLog>()
            .HasOne(s => s.PaymentTransactions)
            .WithMany()
            .HasForeignKey(s => s.PaymentTransactionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SubscriptionLog>()
            .HasOne(s => s.Plans)
            .WithMany(p => p.SubscriptionLogs)
            .HasForeignKey(s => s.NewPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SubscriptionLog>()
            .HasOne<Plan>()
            .WithMany()
            .HasForeignKey(s => s.OldPlanId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ComboDetail>()
    .HasOne(cd => cd.Dish)
    .WithMany(d => d.ComboDetails)
    .HasForeignKey(cd => cd.DishId)
    .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ComboDetail>()
            .HasOne(cd => cd.ItemDish)
            .WithMany()
            .HasForeignKey(cd => cd.ItemDishId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
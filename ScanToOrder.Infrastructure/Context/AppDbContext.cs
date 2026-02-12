using Microsoft.EntityFrameworkCore;
using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Entities.Restaurants;
using System.Reflection;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Entities.Wallet;

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
    public DbSet<AdminWallet> AdminWallet { get; set; } = null!;
    public DbSet<TenantWallet> TenantWallets { get; set; } = null!;
    public DbSet<WalletTransaction> WalletTransactions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
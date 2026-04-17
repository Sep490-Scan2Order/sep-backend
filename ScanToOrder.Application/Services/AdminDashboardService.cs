using ScanToOrder.Application.DTOs.Dashboard;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Entities.Restaurants;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.Services
{
    public class AdminDashboardService : IAdminDashboardService
    {
        private readonly IUnitOfWork _unitOfWork;

        public AdminDashboardService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<SummaryMetricsResponse> GetSummaryMetricsAsync()
        {
            var totalTenants = await _unitOfWork.Tenants.CountAsync(t => true);

            var totalRestaurants = await _unitOfWork.Restaurants.CountAsync(r => true);

            var successfulPayments = await _unitOfWork.PaymentTransactions
                .FindAsync(pt => pt.Status == PaymentTransactionStatus.Success);

            var platformRevenue = await _unitOfWork.PaymentTransactions
    .SumAsync(
        pt => pt.Status == PaymentTransactionStatus.Success,
        pt => pt.TotalAmount
    );

            var activeAccounts = await _unitOfWork.AuthenticationUsers
                .CountAsync(u => u.Role == Role.Tenant);


            return new SummaryMetricsResponse
            {
                TotalTenants = totalTenants,
                TotalRestaurants = totalRestaurants,
                PlatformRevenue = platformRevenue,
                ActiveAccounts = activeAccounts,
            };
        }

        public async Task<List<SubscriptionRevenueTrendDto>> GetSubscriptionRevenueTrendsAsync(int months = 6)
        {
            var startDate = DateTime.UtcNow.AddMonths(-months);

            var rawData = await _unitOfWork.PaymentTransactions
                .GetRevenueTrendRawAsync(startDate, PaymentTransactionType.Subscription);

            var result = new List<SubscriptionRevenueTrendDto>();
            for (int i = months - 1; i >= 0; i--)
            {
                var targetDate = DateTime.UtcNow.AddMonths(-i);
                var match = rawData.FirstOrDefault(x => x.Year == targetDate.Year && x.Month == targetDate.Month);

                result.Add(new SubscriptionRevenueTrendDto
                {
                    Month = $"{targetDate.Month:D2}/{targetDate.Year}",
                    Revenue = match.Revenue > 0 ? match.Revenue : 0
                });
            }

            return result;
        }

        public async Task<List<CommissionFeeRevenueTrendDto>> GetCommissionFeeRevenueTrendsAsync(int months = 6)
        {
            var startDate = DateTime.UtcNow.AddMonths(-months);

            var rawData = await _unitOfWork.PaymentTransactions
                .GetRevenueTrendRawAsync(startDate, PaymentTransactionType.CommissionFee);

            var result = new List<CommissionFeeRevenueTrendDto>();
            for (int i = months - 1; i >= 0; i--)
            {
                var targetDate = DateTime.UtcNow.AddMonths(-i);
                var match = rawData.FirstOrDefault(x => x.Year == targetDate.Year && x.Month == targetDate.Month);

                result.Add(new CommissionFeeRevenueTrendDto
                {
                    Month = $"{targetDate.Month:D2}/{targetDate.Year}",
                    Revenue = match.Revenue > 0 ? match.Revenue : 0
                });
            }

            return result;
        }

        public async Task<List<SubscriptionRevenueByPlanDto>> GetSubscriptionRevenueByPlanAsync(int months = 6)
        {
            var startDate = DateTime.UtcNow.AddMonths(-months);

            var transactions = await _unitOfWork.PaymentTransactions.GetAllAsync(pt =>
                pt.Status == PaymentTransactionStatus.Success
                && pt.PaymentTransactionType == PaymentTransactionType.Subscription
                && pt.PaymentDate >= startDate);

            var revenueByPlan = new Dictionary<int, decimal>();

            foreach (var txn in transactions)
            {
                if (txn.SubscriptionPayload != null)
                {
                    foreach (var item in txn.SubscriptionPayload)
                    {
                        if (!revenueByPlan.ContainsKey(item.NewPlanId))
                        {
                            revenueByPlan[item.NewPlanId] = 0;
                        }
                        revenueByPlan[item.NewPlanId] += item.AmountAllocated;
                    }
                }
            }

            var totalRevenue = revenueByPlan.Values.Sum();
            if (totalRevenue == 0) return new List<SubscriptionRevenueByPlanDto>();

            var planIds = revenueByPlan.Keys.ToList();
            var plansMap = (await _unitOfWork.Plans.FindAsync(p => planIds.Contains(p.Id)))
                .ToDictionary(p => p.Id, p => p.Name);

            var result = new List<SubscriptionRevenueByPlanDto>();
            foreach (var kvp in revenueByPlan)
            {
                plansMap.TryGetValue(kvp.Key, out var planName);
                result.Add(new SubscriptionRevenueByPlanDto
                {
                    PlanId = kvp.Key,
                    PlanName = planName ?? "Không xác định",
                    Revenue = kvp.Value,
                    Percentage = Math.Round((double)(kvp.Value / totalRevenue) * 100, 2)
                });
            }

            return result.OrderByDescending(x => x.Revenue).ToList();
        }

        public async Task<List<SubscriptionPlanDistributionDto>> GetSubscriptionPlanDistributionAsync()
        {
            var rawData = await _unitOfWork.Subscriptions
                .GetSubscriptionDistributionRawAsync();

            var total = rawData.Sum(x => x.Count);

            if (total == 0)
                return new List<SubscriptionPlanDistributionDto>();

            return rawData.Select(x => new SubscriptionPlanDistributionDto
            {
                PlanName = x.PlanName,
                Count = x.Count,
                Percentage = Math.Round((double)x.Count / total * 100, 2)
            }).ToList();
        }

        public async Task<List<TopPerformingRestaurantDto>> GetTopPerformingRestaurantsAsync(int top = 5)
        {
            var data = await _unitOfWork.Orders.GetTopRestaurantsFullDataAsync(top);

            if (!data.Any())
                return new List<TopPerformingRestaurantDto>();

            return data.Select(x => new TopPerformingRestaurantDto
            {
                RestaurantId = x.RestaurantId,
                RestaurantName = x.RestaurantName,
                AvatarUrl = x.Image,
                TotalOrders = x.TotalOrders,
                TotalRevenue = x.TotalRevenue,
                CurrentPlan = GetPlanName(x.PlanName, x.Status)
            }).ToList();
        }
         
        private string GetPlanName(string? planName, SubscriptionStatus? status)
        {
            if (status != SubscriptionStatus.Active)
                return "No Active Plan";

            return planName ?? "Unknown";
        }
        public async Task<List<ExpiringSubscriptionDto>> GetExpiringSubscriptionsAsync(int daysThreshold = 30)
        {
            var now = DateTime.UtcNow;
            var targetDate = now.AddDays(daysThreshold);

            var rawData = await _unitOfWork.Subscriptions
                .GetExpiringSubscriptionsRawAsync(now, targetDate);

            return rawData.Select(x => new ExpiringSubscriptionDto
            {
                RestaurantId = x.RestaurantId,
                RestaurantName = x.RestaurantName,
                PlanName = x.PlanName,
                ExpirationDate = x.ExpirationDate,
                DaysRemaining = CalculateDaysRemaining(x.ExpirationDate, now)
            }).ToList();
        }
        private int CalculateDaysRemaining(DateTime expirationDate, DateTime now)
        {
            return (expirationDate.Date - now.Date).Days;
        }

        public async Task<List<TopTenantDto>> GetTopTenantsByRevenueAsync(int top = 10)
        {
            var data = await _unitOfWork.Orders.GetTopTenantsByRevenueAsync(top);

            return data.Select(x => new TopTenantDto
            {
                TenantId         = x.TenantId,
                TenantName       = x.TenantName,
                TotalRestaurants = x.TotalRestaurants,
                TotalOrders      = x.TotalOrders,
                TotalRevenue     = x.TotalRevenue
            }).ToList();
        }

        public async Task<TenantDetailDto> GetTenantDetailAsync(Guid tenantId, DateTime startDate, DateTime endDate)
        {
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId)
                ?? throw new DomainException($"Tenant {tenantId} not found.");

            var restaurants = await _unitOfWork.Restaurants
                .GetRestaurantsWithSubscriptionsByTenantIdAsync(tenantId);

            var revenueMap = (await _unitOfWork.Orders
                .GetRevenueByTenantAsync(tenantId, startDate, endDate))
                .ToDictionary(r => r.RestaurantId);

            var restaurantDtos = restaurants.Select(r =>
            {
                revenueMap.TryGetValue(r.Id, out var rev);
                return new RestaurantRevenueDto
                {
                    RestaurantId   = r.Id,
                    RestaurantName = r.RestaurantName,
                    Image          = r.Image,
                    Address        = r.Address,
                    CurrentPlan    = GetPlanName(r.Subscription?.Plan?.Name, r.Subscription?.Status),
                    IsActive       = r.IsActive ?? false,
                    TotalOrders    = rev.TotalOrders,
                    GrossRevenue   = rev.GrossRevenue,
                    NetRevenue     = rev.NetRevenue,
                    TotalDiscount  = rev.TotalDiscount
                };
            }).ToList();

            return new TenantDetailDto
            {
                TenantId   = tenant.Id,
                TenantName = tenant.Name ?? string.Empty,
                IsSuspended = tenant.IsSuspended,
                Period = new ScanToOrder.Application.DTOs.Restaurant.Report.PeriodDto
                {
                    StartDate = startDate,
                    EndDate   = endDate
                },
                Restaurants = restaurantDtos
            };
        }
    }
}

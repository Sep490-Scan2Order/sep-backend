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
                .GetRevenueTrendRawAsync(startDate);

            return rawData.Select(x => new SubscriptionRevenueTrendDto
            {
                Month = $"{x.Month}/{x.Year}",
                Revenue = x.Revenue
            }).ToList();
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

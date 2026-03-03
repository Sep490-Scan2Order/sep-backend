using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Entities.SubscriptionPlan;
using ScanToOrder.Domain.Entities.Wallet;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Xsl;

namespace ScanToOrder.Application.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly IUnitOfWork _unitOfWork;

        public SubscriptionService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<string> SubscribePlanAsync(Guid tenantId, int planId)
        {
            var plan = await _unitOfWork.Plans.GetByIdAsync(planId);
            var wallet = await _unitOfWork.TenantWallets.GetByTenantIdAsync(tenantId);
            var tenant = await _unitOfWork.Tenants.GetByIdAsync(tenantId);

            if (plan == null) throw new DomainException("Gói Plan không tồn tại.");
            if (wallet == null) throw new DomainException("Không tìm thấy ví của người dùng.");
            if (tenant == null) throw new DomainException("Không tìm thấy tenant");

            if (wallet.WalletBalance < plan.Price)
                throw new DomainException("Số dư ví không đủ để mua gói này.");

            var freeAddOn = await _unitOfWork.AddOns.GetByIdAsync(1);
            if (freeAddOn == null)
            {
                throw new DomainException("Hệ thống chưa cấu hình gói Addon miễn phí mặc định.");
            }

            decimal balanceBefore = wallet.WalletBalance;
            wallet.WalletBalance -= plan.Price;
            decimal balanceAfter = wallet.WalletBalance;

            tenant.TotalCategories = freeAddOn.MaxCategoriesCount;
            tenant.TotalDishes = freeAddOn.MaxDishesCount;
            tenant.TotalRestaurants = plan.MaxRestaurantsCount;
            tenant.SubscriptionExpiryDate = DateTime.UtcNow.AddDays(plan.DurationInDays);

            var subscription = new Subscription
            {
                TenantId = tenantId,
                PlanId = planId,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddDays(plan.DurationInDays),
                IsActive = true,
                AddOnId = 1
            };

            var transaction = new WalletTransaction
            {
                TenantWalletId = wallet.Id,
                Amount = plan.Price,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceAfter,
                PaymentDate = DateTime.UtcNow,
                TransactionStatus = TransactionStatus.Success,
                TransactionType = TransactionType.Substract,
                Note = NoteWalletTransaction.PlanSubscription,
                Subscription = subscription,
                OrderCode = 0
            };

            await _unitOfWork.Subscriptions.AddAsync(subscription);
            await _unitOfWork.WalletTransactions.AddAsync(transaction);

            _unitOfWork.TenantWallets.Update(wallet);
            _unitOfWork.Tenants.Update(tenant);

            await _unitOfWork.SaveAsync();

            return "Mua gói thành công";
        }

        public async Task UpgradePlanAsync(Guid tenantId, int newPlanId)
        {
            var now = DateTime.UtcNow;
            var newPlan = (await _unitOfWork.Plans.GetByIdAsync(newPlanId))
                .OrThrow("Plan không tồn tại.");
            var wallet = (await _unitOfWork.TenantWallets.GetByTenantIdAsync(tenantId))
                .OrThrow("Không tìm thấy ví của người dùng.");
            var tenant = (await _unitOfWork.Tenants.GetByIdAsync(tenantId))
                .OrThrow("Không tìm thấy tenant");
            var currentSubscription =
                (await _unitOfWork.Subscriptions.GetByFieldsIncludeAsync(s => s.TenantId == tenantId && s.IsActive && s.EndDate > now,
                    s => s.Plan))
                .OrThrow("Không tìm thấy đăng ký hiện tại.");

            if (newPlan.Id <= currentSubscription.PlanId)
                throw new DomainException("Gói mới phải có cấp độ cao hơn gói hiện tại.");

            var daysRemaining = (currentSubscription.EndDate - now).TotalDays;
            daysRemaining = daysRemaining < 0 ? 0 : daysRemaining;

            decimal oldDailyPrice = currentSubscription.Plan.Price / (decimal)currentSubscription.Plan.DurationInDays;
            decimal newDailyPrice = newPlan.Price / (decimal)newPlan.DurationInDays;
            decimal upgradeCost = Math.Round((newDailyPrice - oldDailyPrice) * (decimal)daysRemaining, 0);
            
            if (wallet.WalletBalance < upgradeCost)
                throw new DomainException("Số dư ví không đủ để mua gói này.");

            // Create wallet transaction for the upgrade
            var newTransaction = new WalletTransaction
            {
                TenantWalletId = wallet.Id,
                Amount = upgradeCost,
                BalanceBefore = wallet.WalletBalance,
                BalanceAfter = wallet.WalletBalance - upgradeCost,
                PaymentDate = now,
                TransactionStatus = TransactionStatus.Success,
                TransactionType = TransactionType.Substract,
                Note = NoteWalletTransaction.PlanUpgrade,
                SubscriptionId = currentSubscription.Id,
                OrderCode = 0
            };

            // Update wallet balance
            wallet.WalletBalance -= upgradeCost;
            wallet.UpdatedAt = now;

            // Update current subscription with new plan
            currentSubscription.PlanId = newPlan.Id;
            currentSubscription.UpdatedAt = now;

            // Update tenant limits based on the new plan
            tenant.TotalRestaurants = newPlan.MaxRestaurantsCount;
            tenant.UpdatedAt = now;
            
            // Save all changes in a single transaction
            await _unitOfWork.WalletTransactions.AddAsync(newTransaction);
            
            _unitOfWork.TenantWallets.Update(wallet);
            _unitOfWork.Subscriptions.Update(currentSubscription);
            _unitOfWork.Tenants.Update(tenant);
            
            await _unitOfWork.SaveAsync();
        }

        public async Task UpgradeAddonAsync(Guid tenantId, int newAddonId)
        {
            var now = DateTime.UtcNow;
            var newAddon = (await _unitOfWork.AddOns.GetByIdAsync(newAddonId))
                .OrThrow("Addon không tồn tại.");
            var wallet = (await _unitOfWork.TenantWallets.GetByTenantIdAsync(tenantId))
                .OrThrow("Không tìm thấy ví của người dùng.");
            var tenant = (await _unitOfWork.Tenants.GetByIdAsync(tenantId))
                .OrThrow("Không tìm thấy tenant");
            var currentSubscription =
                (await _unitOfWork.Subscriptions.GetByFieldsIncludeAsync(s => s.TenantId == tenantId && s.IsActive && s.EndDate > now,
                    s => s.AddOn))
                .OrThrow("Không tìm thấy đăng ký hiện tại.");

            if (currentSubscription.AddOnId >= newAddon.Id)
                throw new DomainException("Addon mới phải có cấp độ cao hơn gói hiện tại.");

            // Calculate prorated cost for the upgrade
            var daysRemaining = (currentSubscription.EndDate - now).TotalDays;
            daysRemaining = daysRemaining < 0 ? 0 : daysRemaining;
            var amountRemaining = (currentSubscription.AddOn.Price / 30) * (decimal)daysRemaining;

            var amountNewDailyAddonPrice = newAddon.Price / 30;
            var upgradeCost = amountNewDailyAddonPrice * (decimal)daysRemaining - amountRemaining;
            upgradeCost = Math.Round(Math.Max(upgradeCost, 0), 0);
            
            if (wallet.WalletBalance < upgradeCost)
                throw new DomainException("Số dư ví không đủ để mua gói này.");

            // Create wallet transaction for the upgrade
            var newTransaction = new WalletTransaction
            {
                TenantWalletId = wallet.Id,
                Amount = upgradeCost,
                BalanceBefore = wallet.WalletBalance,
                BalanceAfter = wallet.WalletBalance - upgradeCost,
                PaymentDate = now,
                TransactionStatus = TransactionStatus.Success,
                TransactionType = TransactionType.Substract,
                Note = NoteWalletTransaction.AddonPurchase,
                SubscriptionId = currentSubscription.Id,
                OrderCode = 0
            };

            // Update wallet balance
            wallet.WalletBalance -= upgradeCost;
            wallet.UpdatedAt = now;

            // Update current subscription with new addon
            currentSubscription.AddOnId = newAddon.Id;

            // Update tenant limits based on the new addon
            tenant.TotalCategories = newAddon.MaxCategoriesCount;
            tenant.TotalDishes = newAddon.MaxDishesCount;
            tenant.UpdatedAt = now;
            
            // Save all changes in a single transaction
            await _unitOfWork.WalletTransactions.AddAsync(newTransaction);
            
            _unitOfWork.Subscriptions.Update(currentSubscription);
            _unitOfWork.TenantWallets.Update(wallet);
            _unitOfWork.Tenants.Update(tenant);
            
            await _unitOfWork.SaveAsync();
        }

        public async Task RenewPreviousSubscription(Guid tenantId)
        {
            var now = DateTime.UtcNow;
            var wallet = (await _unitOfWork.TenantWallets.GetByTenantIdAsync(tenantId))
                .OrThrow("Không tìm thấy ví của người dùng.");
            var tenant = (await _unitOfWork.Tenants.GetByIdAsync(tenantId))
                .OrThrow("Không tìm thấy tenant");
            var currentSubscription =
                (await _unitOfWork.Subscriptions.GetByFieldsIncludeAsync(s => s.TenantId == tenantId && s.IsActive,
                    s => s.Plan, s => s.AddOn))
                .OrThrow("Không tìm thấy đăng ký hiện tại.");
            
            // Calculate total renewal cost (plan price + prorated addon price)
            decimal addonRenewalPrice = (currentSubscription.AddOn.Price / 30) * currentSubscription.Plan.DurationInDays;
            decimal totalRenewalCost = Math.Round(currentSubscription.Plan.Price + addonRenewalPrice, 0);

            // Check if wallet balance is sufficient for renewal
            if (wallet.WalletBalance < totalRenewalCost)
                throw new DomainException($"Số dư ví không đủ để gia hạn. Cần thanh toán: {totalRenewalCost:N0} VNĐ.");

            
            DateTime newEndDate = currentSubscription.EndDate > now
                ? currentSubscription.EndDate.AddDays(currentSubscription.Plan.DurationInDays)
                : now.AddDays(currentSubscription.Plan.DurationInDays);
            
            // Create wallet transaction for the renewal
            var transaction = new WalletTransaction
            {
                TenantWalletId = wallet.Id,
                Amount = totalRenewalCost,
                BalanceBefore = wallet.WalletBalance,
                BalanceAfter = wallet.WalletBalance - totalRenewalCost,
                PaymentDate = now,
                TransactionStatus = TransactionStatus.Success,
                TransactionType = TransactionType.Substract,
                Note = NoteWalletTransaction.PlanRenewal,
                SubscriptionId = currentSubscription.Id
            };
            
            // Update wallet balance
            wallet.WalletBalance -= totalRenewalCost;
            wallet.UpdatedAt = now;
        
            // Revoke current subscription and create new subscription with extended end date
            currentSubscription.IsActive = false;
            currentSubscription.UpdatedAt = now;

            var newSubscription = new Subscription
            {
                TenantId = tenantId,
                PlanId = currentSubscription.PlanId,
                AddOnId = currentSubscription.AddOnId,
                StartDate = currentSubscription.EndDate > now ? currentSubscription.EndDate : now,
                EndDate = newEndDate,
                IsActive = true
            };
            
            // Update tenant's subscription expiry date
            tenant.SubscriptionExpiryDate = newEndDate;
            tenant.UpdatedAt = now;
            
            // Save all changes in a single transaction
            await _unitOfWork.WalletTransactions.AddAsync(transaction);
            await _unitOfWork.Subscriptions.AddAsync(newSubscription);

            _unitOfWork.TenantWallets.Update(wallet);
            _unitOfWork.Subscriptions.Update(currentSubscription);
            _unitOfWork.Tenants.Update(tenant);

            await _unitOfWork.SaveAsync();
        }
    }
}
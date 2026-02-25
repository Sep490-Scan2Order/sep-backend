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
    }
}

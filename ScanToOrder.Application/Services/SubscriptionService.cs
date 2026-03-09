using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly IUnitOfWork _unitOfWork;

    public SubscriptionService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public Task<string> SubscribePlanAsync(Guid tenantId, int planId)
    {
        throw new NotImplementedException();
    }

    public Task UpgradePlanAsync(Guid tenantId, int newPlanId)
    {
        throw new NotImplementedException();
    }

    public Task UpgradeAddonAsync(Guid tenantId, int newAddonId)
    {
        throw new NotImplementedException();
    }

    public Task RenewPreviousSubscription(Guid tenantId)
    {
        throw new NotImplementedException();
    }
    
}
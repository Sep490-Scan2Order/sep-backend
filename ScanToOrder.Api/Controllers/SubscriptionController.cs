using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Exceptions;

namespace ScanToOrder.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SubscriptionController : BaseController
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly IAuthenticatedUserService _authenticatedUserService;

        public SubscriptionController(ISubscriptionService subscriptionService, IAuthenticatedUserService authenticatedUserService)
        {
            _subscriptionService = subscriptionService;
            _authenticatedUserService = authenticatedUserService;
        }

        [Authorize (Roles = "Tenant")]
        [HttpPost]
        public async Task<ActionResult<ApiResponse<string>>> Subscription([FromQuery] int planId)
        {
            if (_authenticatedUserService.ProfileId != null)
            {
                var result = await _subscriptionService.SubscribePlanAsync(_authenticatedUserService.ProfileId.Value ,planId);
                return Success(string.Empty, result);
            }
            throw new DomainException("ProfileId is null");
        }
        
        [Authorize (Roles = "Tenant")]
        [HttpPost("upgrade-plan/{newPlanId:int}")]
        public async Task<ActionResult<ApiResponse<string>>> UpgradeSubscription([FromRoute] int newPlanId)
        {
            if (_authenticatedUserService.ProfileId != null)
            {
                await _subscriptionService.UpgradePlanAsync(_authenticatedUserService.ProfileId.Value ,newPlanId);
                return Success(string.Empty);
            }
            throw new DomainException("ProfileId is null");
        }
        
        [Authorize (Roles = "Tenant")]
        [HttpPost("upgrade-addon/{newAddonId:int}")]
        public async Task<ActionResult<ApiResponse<string>>> UpgradeAddon([FromRoute] int newAddonId)
        {
            if (_authenticatedUserService.ProfileId != null)
            {
                await _subscriptionService.UpgradeAddonAsync(_authenticatedUserService.ProfileId.Value ,newAddonId);
                return Success(string.Empty);
            }
            throw new DomainException("ProfileId is null");
        }
    }
}

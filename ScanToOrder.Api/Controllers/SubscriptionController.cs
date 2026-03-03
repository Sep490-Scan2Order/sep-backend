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
    }
}

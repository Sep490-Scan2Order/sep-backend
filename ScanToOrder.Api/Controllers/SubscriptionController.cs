using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.Plan;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
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

        [HttpPost("preview")]
        [Authorize(Roles = "Tenant")]
        public async Task<ActionResult<ApiResponse<CheckoutPreviewResponse>>> GetCheckoutPreview([FromBody] PlanCheckoutRequest request)
        {
            if (_authenticatedUserService.ProfileId == null) throw new DomainException(AuthMessage.AuthError.USER_PROFILE_NOT_FOUND);
            var tenantId = _authenticatedUserService.ProfileId.Value;
            var previewResponse = await _subscriptionService.CalculatePreviewAsync(request, tenantId);
            return Success(previewResponse);
        }
        
        [HttpPost("create-payment")]
        [Authorize(Roles = "Tenant")]
        public async Task<ActionResult<ApiResponse<string>>> CreatePaymentRequest([FromBody] PlanCheckoutRequest request)
        {
            if (_authenticatedUserService.ProfileId == null) throw new DomainException(AuthMessage.AuthError.USER_PROFILE_NOT_FOUND);
            var tenantId = _authenticatedUserService.ProfileId.Value;
            var paymentResult = await _subscriptionService.CreatePaymentAsync(request, tenantId);
            return Success(paymentResult);
        }
        
        [HttpGet("get-by-tenant")]
        [Authorize(Roles = "Tenant")]
        public async Task<ActionResult<ApiResponse<List<RestaurantSubscriptionDto>>>> GetSubscriptionsByTenantAsync()
        {
            if (_authenticatedUserService.ProfileId == null) throw new DomainException(AuthMessage.AuthError.USER_PROFILE_NOT_FOUND);
            var tenantId = _authenticatedUserService.ProfileId.Value;
            var paymentResult = await _subscriptionService.GetSubscriptionsByTenantAsync(tenantId);
            return Success(paymentResult);
        }
    }
}

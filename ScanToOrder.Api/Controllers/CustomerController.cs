using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanToOrder.Application.DTOs.User;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Exceptions;

namespace ScanToOrder.Api.Controllers
{
    public class CustomerController : BaseController
    {
        private readonly ICustomerService _customerService;
        private readonly IAuthenticatedUserService _authenticatedUserService;
        public CustomerController(ICustomerService customerService, IAuthenticatedUserService authenticatedUserService)
        {
            _customerService = customerService;
            _authenticatedUserService = authenticatedUserService;
        }

        [HttpPut("update-info")]
        [Authorize(Roles = "Customer")]
        public async Task<ActionResult<ApiResponse<CustomerDto>>> UpdateCustomerInfo([FromBody] UpdateCustomerRequestDto requestDto)
        {
            if (!_authenticatedUserService.ProfileId.HasValue)
            {
                throw new DomainException(AuthMessage.AuthError.USER_PROFILE_NOT_FOUND);
            }

            var customerId = _authenticatedUserService.ProfileId.Value;

            var result = await _customerService.UpdateCustomerInfoAsync(requestDto, customerId);

            return Success(result, CustomerMessage.CustomerSuccess.CUSTOMER_UPDATED);
        }
    }
}

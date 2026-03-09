using ScanToOrder.Application.DTOs.User;

namespace ScanToOrder.Application.Interfaces
{
    public interface ICustomerService
    {
        Task<CustomerDto> UpdateCustomerInfoAsync(UpdateCustomerRequestDto requestDto, Guid customerId);
    }
}

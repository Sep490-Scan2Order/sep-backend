using AutoMapper;
using ScanToOrder.Application.DTOs.User;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        public CustomerService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }
        public async Task<CustomerDto> UpdateCustomerInfoAsync(UpdateCustomerRequestDto requestDto, Guid customerId)
        {
            var customer = await _unitOfWork.Customers.FirstOrDefaultAsync(c => c.Id == customerId);
            if (customer == null)
            {
                Console.WriteLine($"DEBUG: Không tìm thấy Customer có ID {customerId} trong Database!");
                throw new Exception(CustomerMessage.CustomerError.CUSTOMER_NOT_FOUND);
            }

            _mapper.Map(requestDto, customer);
            _unitOfWork.Customers.Update(customer);
            await _unitOfWork.SaveAsync();

            return _mapper.Map<CustomerDto>(customer);
        }
    }
}

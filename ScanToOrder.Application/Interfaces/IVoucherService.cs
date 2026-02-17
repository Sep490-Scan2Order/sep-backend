using ScanToOrder.Application.DTOs.Voucher;

namespace ScanToOrder.Application.Interfaces
{
    public interface IVoucherService
    {
        Task<VoucherResponseDto> CreateAsync(CreateVoucherDto request);
        Task<List<VoucherResponseDto>> GetAllAsync();
    }
}

using ScanToOrder.Application.DTOs.Voucher;
using System;

namespace ScanToOrder.Application.Interfaces
{
    public interface IVoucherService
    {
        Task<VoucherResponseDto> CreateAsync(CreateVoucherDto request);
        Task<List<VoucherResponseDto>> GetAllAsync();
        Task<RedeemVoucherResponseDto> RedeemVoucherAsync(Guid accountId, RedeemVoucherRequestDto request);
        Task<List<RedeemVoucherResponseDto>> GetMyVouchersAsync(Guid accountId);
        Task<List<RedeemVoucherResponseDto>> GetMyExpiredVouchersAsync(Guid accountId);
    }
}

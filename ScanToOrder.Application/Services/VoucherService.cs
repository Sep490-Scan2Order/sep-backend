using AutoMapper;
using FluentValidation;
using ScanToOrder.Application.DTOs.Voucher;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Entities.Points;
using ScanToOrder.Domain.Entities.Vouchers;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScanToOrder.Application.Services
{
    public class VoucherService : IVoucherService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IValidator<CreateVoucherDto> _createValidator;

        public VoucherService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IValidator<CreateVoucherDto> createValidator)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _createValidator = createValidator;
        }

        public async Task<VoucherResponseDto> CreateAsync(CreateVoucherDto request)
        {
            var validationResult = await _createValidator.ValidateAsync(request);

            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }

            var entity = _mapper.Map<Voucher>(request);

            entity.Status = VoucherStatus.Active;
            entity.CreatedAt = DateTime.UtcNow;
            entity.IsDeleted = false;

            await _unitOfWork.Vouchers.AddAsync(entity);
            await _unitOfWork.SaveAsync();

            return _mapper.Map<VoucherResponseDto>(entity);
        }

        public async Task<List<VoucherResponseDto>> GetAllAsync()
        {
            var vouchers = await _unitOfWork.Vouchers.GetAllAsync();
            return _mapper.Map<List<VoucherResponseDto>>(vouchers);
        }

        public async Task<RedeemVoucherResponseDto> RedeemVoucherAsync(Guid accountId, RedeemVoucherRequestDto request)
        {
            var memberPoint = await _unitOfWork.MemberPoints.GetByAccountIdAsync(accountId);
            if (memberPoint == null)
                throw new InvalidOperationException("Không tìm thấy tài khoản điểm của bạn.");

            var voucher = await _unitOfWork.Vouchers.GetByIdAsync(request.VoucherId);
            if (voucher == null)
                throw new InvalidOperationException("Voucher không tồn tại.");
            if (voucher.Status != VoucherStatus.Active)
                throw new InvalidOperationException("Voucher không khả dụng.");
            if (memberPoint.CurrentPoint < voucher.PointRequire)
                throw new InvalidOperationException($"Điểm không đủ. Cần {voucher.PointRequire} điểm, hiện có {memberPoint.CurrentPoint} điểm.");

            const int voucherValidDays = 30;
            var memberVoucher = new MemberVoucher
            {
                UserId = memberPoint.CustomerId,
                VoucherId = voucher.Id,
                IsUsed = false,
                ExpiredAt = DateTime.UtcNow.AddDays(voucherValidDays),
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };
            await _unitOfWork.MemberVouchers.AddAsync(memberVoucher);
            await _unitOfWork.SaveAsync();

            var pointHistory = new PointHistory
            {
                Point = voucher.PointRequire,
                Type = PointHistoryType.Spend,
                CreateDate = DateTime.UtcNow,
                MemberPointId = memberPoint.MemberPointId,
                MemberVoucherId = memberVoucher.Id
            };
            memberPoint.CurrentPoint -= voucher.PointRequire;
            await _unitOfWork.PointHistories.AddAsync(pointHistory);
            await _unitOfWork.SaveAsync();

            memberVoucher.Voucher = voucher;
            return _mapper.Map<RedeemVoucherResponseDto>(memberVoucher);
        }

        public async Task<List<RedeemVoucherResponseDto>> GetMyVouchersAsync(Guid accountId)
        {
            var memberPoint = await _unitOfWork.MemberPoints.GetByAccountIdAsync(accountId);
            if (memberPoint == null)
                throw new InvalidOperationException("Không tìm thấy tài khoản điểm của bạn.");

            var now = DateTime.UtcNow;

            var memberVouchers = await _unitOfWork.MemberVouchers
                .GetActiveByUserIdAsync(memberPoint.CustomerId, now);

            return _mapper.Map<List<RedeemVoucherResponseDto>>(memberVouchers);
        }

        public async Task<List<RedeemVoucherResponseDto>> GetMyExpiredVouchersAsync(Guid accountId)
        {
            var memberPoint = await _unitOfWork.MemberPoints.GetByAccountIdAsync(accountId);
            if (memberPoint == null)
                throw new InvalidOperationException("Không tìm thấy tài khoản điểm của bạn.");

            var now = DateTime.UtcNow;

            var memberVouchers = await _unitOfWork.MemberVouchers
                .GetExpiredByUserIdAsync(memberPoint.CustomerId, now);

            return _mapper.Map<List<RedeemVoucherResponseDto>>(memberVouchers);
        }
    }
}

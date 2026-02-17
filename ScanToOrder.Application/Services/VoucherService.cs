using AutoMapper;
using FluentValidation;
using ScanToOrder.Application.DTOs.Voucher;
using ScanToOrder.Application.Interfaces;
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
        private readonly IValidator<CreateVoucherDto> _validator;
        public VoucherService(IUnitOfWork unitOfWork, IMapper mapper, IValidator<CreateVoucherDto> validator)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _validator = validator;
        }
        public async Task<VoucherResponseDto> CreateAsync(CreateVoucherDto request)
        {
            var validationResult = await _validator.ValidateAsync(request);

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
    }
}

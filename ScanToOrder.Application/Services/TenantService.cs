using AutoMapper;
using ScanToOrder.Application.DTOs.External;
using ScanToOrder.Application.DTOs.User;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Entities.Authentication;
using ScanToOrder.Domain.Entities.User;
using ScanToOrder.Domain.Interfaces;
using System.Net.Http.Json;

namespace ScanToOrder.Application.Services
{
    public class TenantService : ITenantService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly ITaxService _taxService; 

        public TenantService(IUnitOfWork unitOfWork, IMapper mapper, ITaxService taxService)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _taxService = taxService;
        }

        public async Task<TenantDto> RegisterTenantAsync(RegisterTenantRequest request)
        {
            if (!string.IsNullOrEmpty(request.TaxNumber))
            {
                var isValid = await _taxService.IsTaxCodeValidAsync(request.TaxNumber);
                if (!isValid) throw new Exception("Mã số thuế không hợp lệ hoặc đã ngừng hoạt động.");
            }

            var userEntity = _mapper.Map<AuthenticationUser>(request);
            userEntity.Password = request.Password;

            var tenantEntity = _mapper.Map<Tenant>(request);

            tenantEntity.AccountId = userEntity.Id;

            await _unitOfWork.AuthenticationUsers.AddAsync(userEntity);
            await _unitOfWork.Tenants.AddAsync(tenantEntity);

            await _unitOfWork.SaveAsync();

            return _mapper.Map<TenantDto>(tenantEntity);
        }
    }
}

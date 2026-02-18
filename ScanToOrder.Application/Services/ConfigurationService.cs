using AutoMapper;
using ScanToOrder.Application.DTOs.Configuration;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Entities.Configuration;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services
{
    public class ConfigurationService : IConfigurationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        public ConfigurationService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }
        public async Task<ConfigurationResponse> GetConfigurationsAsync()
        {
            var configurations = await _unitOfWork.Configurations.GetAllAsync();
            return _mapper.Map<ConfigurationResponse>(configurations.FirstOrDefault());
        }
        public async Task<ConfigurationResponse> UpdateConfigurationsAsync(Configurations configurations)
        {
            var existingConfig = (await _unitOfWork.Configurations.GetAllAsync()).FirstOrDefault();
            if (existingConfig == null)
            {
                await _unitOfWork.Configurations.AddAsync(configurations);
            }
            else
            {
                existingConfig.VoucherRate = configurations.VoucherRate;
                existingConfig.CommissionRate = configurations.CommissionRate;
                existingConfig.ExpiredDuration = configurations.ExpiredDuration;
                existingConfig.RedeemRate = configurations.RedeemRate;
                existingConfig.LastUpdated = DateOnly.FromDateTime(DateTime.UtcNow);
                _unitOfWork.Configurations.Update(existingConfig);
            }
            await _unitOfWork.SaveAsync();
            return _mapper.Map<ConfigurationResponse>(configurations);
        }
    }
}

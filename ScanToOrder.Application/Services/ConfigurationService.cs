using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Wrapper;
using ScanToOrder.Domain.Entities.Configuration;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services
{
    public class ConfigurationService : IConfigurationService
    {
        private readonly IUnitOfWork _unitOfWork;
        public ConfigurationService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }
        public async Task<ApiResponse<Configurations>> GetConfigurationsAsync()
        {
            var configurations = await _unitOfWork.Configurations.GetAllAsync();
            return new ApiResponse<Configurations>
            {
                IsSuccess = true,
                Data = configurations.FirstOrDefault() ?? new Configurations()
            };
        }
        public async Task<ApiResponse<Configurations>> UpdateConfigurationsAsync(Configurations configurations)
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
            return new ApiResponse<Configurations>
            {
                IsSuccess = true,
                Data = configurations
            };
        }
    }
}

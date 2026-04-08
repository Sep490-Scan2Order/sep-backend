using AutoMapper;
using ScanToOrder.Application.DTOs.Configuration;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Domain.Entities.Configuration;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ConfigurationService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ConfigurationResponse?> GetConfigurationsAsync()
    {
        var row = (await _unitOfWork.Configurations.GetAllAsync()).FirstOrDefault();
        return _mapper.Map<ConfigurationResponse?>(row);
    }

    public async Task<ConfigurationResponse> UpdateConfigurationsAsync(int id, UpdateConfigurationRequest request)
    {
        var existing = await _unitOfWork.Configurations.GetByIdAsync(id);
        Configurations result;

        if (existing == null)
        {
            var utc = DateTime.UtcNow;
            result = new Configurations
            {
                Id = id,
                CommissionRate = request.CommissionRate,
                CreatedAt = utc,
                UpdatedAt = utc,
                IsDeleted = false
            };
            await _unitOfWork.Configurations.AddAsync(result);
        }
        else
        {
            existing.CommissionRate = request.CommissionRate;
            existing.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Configurations.Update(existing);
            result = existing;
        }

        await _unitOfWork.SaveAsync();
        return _mapper.Map<ConfigurationResponse>(result);
    }
}

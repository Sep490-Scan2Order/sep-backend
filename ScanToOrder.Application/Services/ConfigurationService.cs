using AutoMapper;
using ScanToOrder.Application.DTOs.Configuration;
using ScanToOrder.Application.Interfaces;
using ScanToOrder.Application.Message;
using ScanToOrder.Application.Template;
using ScanToOrder.Domain.Exceptions;
using ScanToOrder.Domain.Interfaces;

namespace ScanToOrder.Application.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly IEmailService _emailService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ITenantService _tenantService;
    private readonly IMapper _mapper;

    public ConfigurationService(IUnitOfWork unitOfWork, IMapper mapper, IEmailService emailService, ITenantService tenantService)
    {
        _unitOfWork = unitOfWork;
        _mapper = mapper;
        _emailService = emailService;
        _tenantService = tenantService;
    }

    public async Task<ConfigurationResponse?> GetConfigurationsAsync()
    {
        var row = (await _unitOfWork.Configurations.GetAllAsync()).FirstOrDefault();
        return _mapper.Map<ConfigurationResponse?>(row);
    }

    public async Task<ConfigurationResponse> UpdateConfigurationsAsync(int id, UpdateConfigurationRequest request)
    {
        var existing = await _unitOfWork.Configurations.GetByIdAsync(id)
            ?? throw new DomainException($"Không tìm thấy cấu hình với ID: {id}");

        existing.CommissionRate = request.CommissionRate;
        existing.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.Configurations.Update(existing);

        await _unitOfWork.SaveAsync();

        var tenants = await _tenantService.GetAllTenantsAsync();
        var emailList = tenants
            .Where(t => !string.IsNullOrEmpty(t.Email))
            .Select(t => t.Email!)
            .ToList();

        if (emailList.Any())
        {
            var templateData = new { request.CommissionRate };

            await _emailService.SendEmailsWithTemplateIdDomainAsync(
                emailList, 
                EmailMessage.EmailSubject.UPDATE_CONFIGURATION_SUBJECT,
                ResendTemplate.UPDATE_CONFIGURATION_TEMPLATE_ID,
                templateData);
        }

        return _mapper.Map<ConfigurationResponse>(existing);
    }
}

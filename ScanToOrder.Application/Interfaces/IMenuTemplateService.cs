using ScanToOrder.Application.DTOs.Menu;

namespace ScanToOrder.Application.Interfaces
{
    public interface IMenuTemplateService
    {
        Task<CreateTemplateResponseDto> CreateTemplateAsync(CreateTemplateRequestDto request);
        Task<IEnumerable<MenuTemplateDto>> GetTemplatesAsync();
    }
}

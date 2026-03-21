using ScanToOrder.Application.DTOs.Menu;

namespace ScanToOrder.Application.Interfaces
{
    public interface IMenuTemplateService
    {
        Task<CreateTemplateResponseDto> CreateTemplateAsync(CreateTemplateRequestDto request);
        Task<IEnumerable<MenuTemplateDto>> GetTemplatesAsync();
        Task<MenuTemplateDto> GetTemplateByIdAsync(int templateId);
        Task<MenuTemplateDto> UpdateTemplateAsync(int templateId, UpdateMenuTemplateDto request);
        Task<MenuTemplateRenderDto> GetRestaurantMenuFromTemplateAsync(int restaurantId);
        Task<AiHolidayTemplateResponseDto> GenerateHolidayThemeAsync(AiHolidayTemplateRequestDto request);
    }
}

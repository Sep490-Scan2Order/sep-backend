using ScanToOrder.Application.DTOs.Promotion;

namespace ScanToOrder.Application.Interfaces;

public interface IPromotionService
{
    Task CreatePromotionAsync(Guid tenantId, CreatePromotionDto dto);
    Task<PromotionResponseDto> GetPromotionByIdAsync(int id);
    Task<PromotionResponseDto> GetPromotionByTenantAsync(Guid tenantId);
    Task UpdatePromotionAsync(UpdatePromotionDto dto);
    Task DeletePromotionAsync(int id);
}
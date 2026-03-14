using ScanToOrder.Application.DTOs.Promotion;
using ScanToOrder.Domain.Entities;

namespace ScanToOrder.Application.Interfaces;

public interface IPromotionService
{
    Task CreatePromotionAsync(Guid tenantId, CreatePromotionDto dto);
    Task<PromotionResponseDto> GetPromotionByIdAsync(int id);
    Task<PagedResult<PromotionResponseDto>> GetPromotionsByTenantAsync(Guid tenantId, int pageNumber = 1, int pageSize = 10);
    Task UpdatePromotionAsync(UpdatePromotionDto dto);
    Task DeletePromotionAsync(int id);
}
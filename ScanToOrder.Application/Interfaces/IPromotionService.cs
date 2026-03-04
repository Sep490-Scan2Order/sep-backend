using ScanToOrder.Application.DTOs.Promotion;

namespace ScanToOrder.Application.Interfaces;

public interface IPromotionService
{
    Task CreatePromotionAsync(Guid tenantId, CreatePromotionDto dto);
}
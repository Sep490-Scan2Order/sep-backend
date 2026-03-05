namespace ScanToOrder.Application.DTOs.Promotion;

public class PromotionResponseDto : CreatePromotionDto
{
    public int Id { get; set; }
    public bool IsActive { get; set; }
}
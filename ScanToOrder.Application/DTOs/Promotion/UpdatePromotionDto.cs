namespace ScanToOrder.Application.DTOs.Promotion;

public class UpdatePromotionDto : CreatePromotionDto
{
    public int Id { get; set; }
    public bool IsActive { get; set; }
}
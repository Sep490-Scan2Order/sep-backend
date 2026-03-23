using System;

namespace ScanToOrder.Application.DTOs.Orders;

public class PaymentQrRequest
{
    public string CartId { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;

    // Preorder chỉ hỗ trợ bank transfer (không dùng cash).
    public bool IsPreOrder { get; set; }

    // Giờ khách chọn (đề xuất) khi preorder; lưu UTC trong DB.
    public DateTime? RequestedPickupAt { get; set; }
}


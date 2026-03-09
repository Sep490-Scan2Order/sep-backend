using System;

namespace ScanToOrder.Application.DTOs.Orders;

public class AddToCartRequest
{
    /// <summary>
    /// Id của nhà hàng mà khách đang đặt (lấy từ QR/menu).
    /// </summary>
    public int RestaurantId { get; set; }

    /// <summary>
    /// Id của món ăn (Dish) trong menu của nhà hàng.
    /// </summary>
    public int DishId { get; set; }

    /// <summary>
    /// Số lượng muốn thêm.
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Ghi chú thêm cho món (tùy chọn).
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Id của Cart đang mở (nếu FE đã có). Nếu null sẽ tạo Cart mới.
    /// </summary>
    public string? CartId { get; set; }
}


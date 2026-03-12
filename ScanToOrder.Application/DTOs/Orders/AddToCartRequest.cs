using System;

namespace ScanToOrder.Application.DTOs.Orders;

public class AddToCartRequest
{
    public int RestaurantId { get; set; }

    public int DishId { get; set; }

    public int Quantity { get; set; } = 1;

    public string NumberPhone { get; set; } = null!;    

    public string? Note { get; set; }

    public string? CartId { get; set; }
}


namespace ScanToOrder.Application.DTOs.Orders
{
    public class CustomerOrderDetailDto
    {
        public int DishId { get; set; }
        public string DishName { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;

        /// <summary>Số lượng còn lại sau hoàn (Quantity - RefundedQuantity).</summary>
        public int Quantity { get; set; }

        /// <summary>Số lượng đặt ban đầu trên dòng đơn.</summary>
        public int OrderedQuantity { get; set; }

        /// <summary>Số lượng đã hoàn tiền trên dòng này.</summary>
        public int RefundedQuantity { get; set; }

        public decimal OriginalPrice { get; set; }
        public decimal DiscountedPrice { get; set; }

        /// <summary>Tổng tiền dòng còn lại (theo tỷ lệ số lượng còn).</summary>
        public decimal SubTotal { get; set; }

        /// <summary>Tổng tiền dòng tại thời điểm đặt (SubTotal gốc trên OrderDetail).</summary>
        public decimal OriginalSubTotal { get; set; }
    }
}


namespace ScanToOrder.Application.Message
{
    public partial class RestaurantMessage
    {
        public class RestaurantError
        {
            public const string RESTAURANT_NOT_FOUND = "Nhà hàng không tồn tại.";
            public const string RESTAURANT_ALREADY_EXISTS = "Nhà hàng đã tồn tại.";
            public const string RESTAURANT_CREATION_FAILED = "Tạo nhà hàng thất bại.";
            public const string RESTAURANT_UPDATE_FAILED = "Cập nhật nhà hàng thất bại.";
            public const string RESTAURANT_DELETE_FAILED = "Xóa nhà hàng thất bại.";
            public const string NOT_FOUND_RESTAURANT_FOR_USER = "Không tìm thấy thông tin định danh người dùng (ProfileId).";
        }

        public class RestaurantSuccess
        {
            public const string RESTAURANT_CREATED = "Tạo nhà hàng thành công.";
            public const string RESTAURANT_UPDATED = "Cập nhật nhà hàng thành công.";
            public const string RESTAURANT_DELETED = "Xóa nhà hàng thành công.";
        }
    }
}

namespace ScanToOrder.Application.Message
{
    public partial class MenuTemplateMessage
    {
        public class MenuTemplateError
        {
            public const string TEMPLATE_NOT_FOUND = "Không tìm thấy Menu Template.";
            public const string TEMPLATE_EXIST_WITH_RESTAURANT = "Menu Template đã được áp dụng cho nhà hàng này.";
            public const string MENU_RESTAURANT_NOT_FOUND = "Không tìm thấy Menu Restaurant.";
            public const string PLAN_FEATURE_CUSTOM_MENU_REQUIRED = "Gói dịch vụ hiện tại không hỗ trợ tính năng Tuỳ chỉnh Menu Template. Vui lòng nâng cấp gói để sử dụng.";
        }
    }
}

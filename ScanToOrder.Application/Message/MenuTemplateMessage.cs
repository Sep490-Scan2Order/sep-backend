namespace ScanToOrder.Application.Message
{
    public partial class MenuTemplateMessage
    {
        public class MenuTemplateError
        {
            public const string TEMPLATE_NOT_FOUND = "Không tìm thấy Menu Template.";
            public const string TEMPLATE_EXIST_WITH_RESTAURANT = "Menu Template đã được áp dụng cho nhà hàng này.";
        }
    }
}

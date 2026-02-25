namespace ScanToOrder.Application.Message
{
    public partial class QrMessage
    {
        public class QrError
        {
            public const string NO_RESTAURANT_FOUND_TO_GENERATE_QR = "Không tìm thấy nhà hàng để tạo mã QR.";
            public const string FILE_IS_EMPTY = "File không được để trống.";
        }
    }
}

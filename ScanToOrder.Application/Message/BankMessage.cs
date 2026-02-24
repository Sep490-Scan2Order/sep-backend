namespace ScanToOrder.Application.Message
{
    public partial class BankMessage
    {
        public class BankSuccess
        {
            public const string BANK_ADDED = "Thêm ngân hàng thành công.";
            public const string BANK_UPDATED = "Cập nhật ngân hàng thành công.";
        }
        public class BankError
        {
            public const string BANK_NOT_FOUND = "Không tìm thấy ngân hàng.";
            public const string BANK_ALREADY_EXISTS = "Ngân hàng đã tồn tại.";
        }
    }
}

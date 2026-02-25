namespace ScanToOrder.Application.Message
{
    public partial class StorageMessage
    {
        public class StorageError
        {
            public const string FILE_IS_EMPTY = "File không được để trống.";
            public const string UPLOAD_FAILED = "Lỗi khi tải ảnh QR lên Supabase";
            public const string INVALID_FILE_TYPE = "Loại file không hợp lệ. Vui lòng tải lên file ảnh.";
        }
    }
}

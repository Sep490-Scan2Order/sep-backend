using ScanToOrder.Domain.Entities.Base;
using ScanToOrder.Domain.Enums;
using ScanToOrder.Domain.Exceptions;

namespace ScanToOrder.Domain.Entities.Promotions
{
    public class Promotion : BaseEntity<int>
    {
        public Guid TenantId { get; set; }
        public string Name { get; set; } = null!;
        public bool IsActive { get; set; } = true;
        public bool IsGlobal { get; set; }

        public PromotionType Type { get; set; } 

        public DiscountType DiscountType { get; set; } 
        public decimal DiscountValue { get; set; }
        public decimal? MaxDiscountValue { get; set; }
        public decimal MinOrderValue { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public TimeSpan? DailyStartTime { get; set; }
        public TimeSpan? DailyEndTime { get; set; }

        public DaysOfWeek DaysOfWeek { get; set; }

        /// <summary>
        /// Kiểm tra tính hợp lệ của dữ liệu khuyến mãi trước khi Lưu/Cập nhật.
        /// <para>
        /// Hàm này đảm bảo các quy tắc nghiệp vụ riêng cho từng loại khuyến mãi:
        /// - <see cref="PromotionType.HappyHour"/>: Bắt buộc có giờ bắt đầu/kết thúc và giờ hợp lệ.
        /// - <see cref="PromotionType.WeeklySpecial"/>: Bắt buộc phải chọn ít nhất 1 ngày trong tuần.
        /// - <see cref="PromotionType.Clearance"/>: Bắt buộc có ngày kết thúc (để tạo tính khan hiếm).
        /// </para>
        /// </summary>
        /// <exception cref="DomainException">Ném lỗi nếu dữ liệu không thỏa mãn logic nghiệp vụ.</exception>
        public void Validate()
        {
            switch (Type)
            {
                case PromotionType.HappyHour:
                    if (!DailyStartTime.HasValue || !DailyEndTime.HasValue)
                        throw new DomainException("Khuyến mãi Giờ vàng bắt buộc phải có khung giờ (Từ mấy giờ -> Mấy giờ).");
                    if (DailyStartTime >= DailyEndTime)
                        throw new DomainException("Giờ kết thúc phải lớn hơn giờ bắt đầu.");
                    break;

                case PromotionType.WeeklySpecial:
                    if (DaysOfWeek == DaysOfWeek.None)
                        throw new DomainException("Khuyến mãi Tuần hoàn bắt buộc phải chọn ít nhất một ngày trong tuần.");
                    break;

                case PromotionType.Clearance:
                    if (!EndDate.HasValue)
                        throw new DomainException("Chương trình Xả hàng bắt buộc phải có ngày kết thúc.");
                    break;
            }
        }

        /// <summary>
        /// Kiểm tra xem khuyến mãi có ĐƯỢC PHÉP áp dụng tại thời điểm cụ thể (<paramref name="now"/>) hay không.
        /// <para>
        /// Quy trình kiểm tra:
        /// 1. Check trạng thái Active và IsDeleted.
        /// 2. Check khoảng thời gian chung (StartDate -> EndDate).
        /// 3. Check điều kiện sâu hơn tùy loại:
        ///    - Nếu là <see cref="PromotionType.HappyHour"/>: Check xem <paramref name="now"/> có nằm trong khung giờ vàng không.
        ///    - Nếu là <see cref="PromotionType.WeeklySpecial"/>: Check xem hôm nay (Thứ 2, 3...) có nằm trong danh sách ngày được chọn không.
        /// </para>
        /// </summary>
        /// <param name="now">Thời điểm cần kiểm tra (thường là DateTime.UtcNow hoặc giờ server).</param>
        /// <returns>True nếu khuyến mãi đang có hiệu lực, False nếu không.</returns>
        public bool IsValidAt(DateTime now)
        {
            if (!IsActive || IsDeleted) return false;

            if (StartDate.HasValue && now < StartDate.Value) return false;
            if (EndDate.HasValue && now > EndDate.Value) return false;

            switch (Type)
            {
                case PromotionType.HappyHour:
                    if (DailyStartTime.HasValue && DailyEndTime.HasValue)
                    {
                        var timeNow = now.TimeOfDay;
                        if (timeNow < DailyStartTime.Value || timeNow > DailyEndTime.Value)
                            return false;
                    }
                    break;

                case PromotionType.WeeklySpecial:
                    var currentDayFlag = (DaysOfWeek)(1 << (int)now.DayOfWeek);
                    if ((DaysOfWeek & currentDayFlag) == 0) return false;
                    break;
            }

            return true;
        }
    }
}

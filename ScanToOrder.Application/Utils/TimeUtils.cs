using System;

namespace ScanToOrder.Application.Utils
{
    public static class TimeUtils
    {
        public static readonly string VietnamTimeZoneId = "SE Asia Standard Time";
        
        public static DateTime GetVietnamTimeNow()
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(VietnamTimeZoneId);
                return TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
            }
            catch (TimeZoneNotFoundException)
            {
                return DateTime.UtcNow.AddHours(7);
            }
        }
        public static (DateTime StartUtc, DateTime EndUtc, int DateInt) GetVietnamDayRangeUtc()
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(VietnamTimeZoneId);
                var nowVn = TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz);
                var vnDate = nowVn.Date;
                var startUtc = TimeZoneInfo.ConvertTimeToUtc(vnDate, tz);
                var endUtc = TimeZoneInfo.ConvertTimeToUtc(vnDate.AddDays(1), tz);
                int dateInt = (vnDate.Year * 10000) + (vnDate.Month * 100) + vnDate.Day;
                return (startUtc, endUtc, dateInt);
            }
            catch
            {
                var utcDate = DateTime.UtcNow.Date;
                int dateInt = (utcDate.Year * 10000) + (utcDate.Month * 100) + utcDate.Day;
                return (utcDate, utcDate.AddDays(1), dateInt);
            }
        }
    }
}

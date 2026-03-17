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
    }
}

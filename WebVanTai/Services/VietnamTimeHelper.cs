using System;

namespace WebVanTai.Services
{
    /// <summary>
    /// Tiện ích quản lý múi giờ Việt Nam (UTC+7, SE Asia Standard Time / Asia/Ho_Chi_Minh).
    /// Đảm bảo tính toán thời gian chính xác trên cả môi trường Windows, Linux và Docker Container.
    /// </summary>
    public static class VietnamTimeHelper
    {
        private static readonly Lazy<TimeZoneInfo> _vietnamTimeZone = new(() =>
        {
            foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(id);
                }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }
            return TimeZoneInfo.CreateCustomTimeZone("UTC+7", TimeSpan.FromHours(7), "VN", "VN");
        });

        /// <summary>
        /// Múi giờ Việt Nam (UTC+7).
        /// </summary>
        public static TimeZoneInfo TimeZone => _vietnamTimeZone.Value;

        /// <summary>
        /// Thời gian hiện tại theo múi giờ Việt Nam (UTC+7).
        /// </summary>
        public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZone);

        /// <summary>
        /// Ngày hôm nay (00:00:00) theo múi giờ Việt Nam.
        /// </summary>
        public static DateTime Today => Now.Date;

        /// <summary>
        /// Kiểm tra xem thời điểm đã qua so với thời gian hiện tại ở Việt Nam chưa.
        /// </summary>
        public static bool IsPast(DateTime dateTime) => dateTime <= Now;

        /// <summary>
        /// Kiểm tra xem một chuyến xe đã khởi hành chưa.
        /// </summary>
        public static bool HasDeparted(DateTime departureTime) => departureTime <= Now;
    }
}

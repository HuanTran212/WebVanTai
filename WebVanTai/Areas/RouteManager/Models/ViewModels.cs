using System.ComponentModel.DataAnnotations;
using WebVanTai.Models;
using WebVanTai.Models.Enums;
using Route = WebVanTai.Models.Route;

namespace WebVanTai.Areas.RouteManager.Models
{
    public class DashboardViewModel
    {
        public int TotalStations { get; set; }
        public int TotalRoutes { get; set; }
        public int TotalBusTypes { get; set; }
        public int TotalBuses { get; set; }
        public int TotalTrips { get; set; }
        public int ScheduledTrips { get; set; }
        public int RunningTrips { get; set; }
        public List<Trip> RecentTrips { get; set; } = new();
    }

    public class RouteFormViewModel
    {
        public int RouteId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tỉnh/thành xuất phát")]
        [Display(Name = "Tỉnh/Thành xuất phát")]
        [StringLength(100)]
        public string StartProvince { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tỉnh/thành đến")]
        [Display(Name = "Tỉnh/Thành đến")]
        [StringLength(100)]
        public string EndProvince { get; set; } = string.Empty;

        [Range(1, 5000, ErrorMessage = "Khoảng cách phải từ 1 đến 5000 km")]
        [Display(Name = "Khoảng cách (km)")]
        public int DistanceKm { get; set; }

        [Range(0.1, 100.0, ErrorMessage = "Thời gian di chuyển phải từ 0.1 đến 100 giờ")]
        [Display(Name = "Thời gian ước tính (giờ)")]
        public decimal DurationHours { get; set; }

        [Display(Name = "Đang hoạt động")]
        public bool IsActive { get; set; } = true;

        public List<RouteStationItemViewModel> RouteStations { get; set; } = new();
    }

    public class RouteStationItemViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn trạm")]
        [Display(Name = "Trạm dừng")]
        public int StationId { get; set; }

        public string StationName { get; set; } = string.Empty;

        [Required]
        [Range(1, 100, ErrorMessage = "Thứ tự dừng phải từ 1")]
        [Display(Name = "Thứ tự dừng (StopOrder)")]
        public int StopOrder { get; set; }

        [Display(Name = "Loại trạm")]
        public StationType StationType { get; set; } = StationType.Both;

        [Range(0, 10000, ErrorMessage = "Thời gian bù (phút) không hợp lệ")]
        [Display(Name = "Phút dừng từ khi xuất bến")]
        public int OffsetMinutes { get; set; }
    }

    /// <summary>
    /// Form xếp lịch chuyến xe mới — hỗ trợ tạo 1 chuyến đơn hoặc
    /// chạy lặp lại hàng ngày theo khung giờ đến ngày kết thúc (tối đa 90 ngày).
    /// </summary>
    public class TripCreateViewModel
    {
        [Required(ErrorMessage = "Vui lòng chọn tuyến đường")]
        [Display(Name = "Tuyến đường")]
        public int RouteId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn xe khách")]
        [Display(Name = "Xe khách")]
        public int BusId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn thời gian khởi hành")]
        [Display(Name = "Thời gian khởi hành")]
        public DateTime DepartureTime { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn thời gian đến")]
        [Display(Name = "Thời gian đến")]
        public DateTime ArrivalTime { get; set; }

        [Range(10000, 100000000, ErrorMessage = "Giá vé tối thiểu 10.000 đ")]
        [Display(Name = "Giá vé cơ bản")]
        public decimal BasePrice { get; set; } = 250000m;

        [Display(Name = "Trạng thái")]
        public TripStatus Status { get; set; } = TripStatus.Scheduled;

        [Display(Name = "Chạy lặp lại hàng ngày")]
        public bool IsDailyRecurring { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Lặp lại đến hết ngày")]
        public DateTime? RepeatEndDate { get; set; }

        [Display(Name = "Tự động bỏ qua những ngày xe bị trùng lịch")]
        public bool SkipConflictingDates { get; set; }
    }
}

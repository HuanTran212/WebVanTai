using System.ComponentModel.DataAnnotations;

namespace WebVanTai.Areas.Admin.ViewModels
{
    /// <summary>
    /// Bảng điều khiển thống kê doanh thu & tỷ lệ lấp đầy ghế cho Admin.
    /// Khoảng thời gian lọc theo ngày tạo đặt chỗ (Booking.CreatedAt) và ngày khởi hành (Trip.DepartureTime).
    /// </summary>
    public class RevenueDashboardViewModel
    {
        public DateTime FromDate { get; set; }

        public DateTime ToDate { get; set; }

        // ===== KPI =====
        /// <summary>Tổng doanh thu (Booking.Status = Paid trong khoảng).</summary>
        public decimal TotalRevenue { get; set; }

        public int TotalPaidBookings { get; set; }

        /// <summary>Số vé bán ra (Booking Paid, Ticket != Void).</summary>
        public int TotalTicketsSold { get; set; }

        /// <summary>Số vé đã soát lên xe.</summary>
        public int TotalCheckedIn { get; set; }

        /// <summary>Tỷ lệ lấp đầy ghế trung bình toàn hệ thống trong khoảng (%).</summary>
        public double OverallOccupancyPercent { get; set; }

        public int TotalSeatsAvailable { get; set; }

        public int TotalTripsInRange { get; set; }

        // ===== Biểu đồ & bảng =====
        public List<RouteRevenueItem> RouteRevenues { get; set; } = new();

        public List<DailyRevenueItem> DailyRevenues { get; set; } = new();

        public string RangeLabel =>
            FromDate.Date == ToDate.Date
                ? FromDate.ToString("dd/MM/yyyy")
                : $"{FromDate:dd/MM/yyyy} – {ToDate:dd/MM/yyyy}";
    }

    /// <summary>Thống kê doanh thu & lấp đầy của một tuyến đường.</summary>
    public class RouteRevenueItem
    {
        public int RouteId { get; set; }

        public string RouteName { get; set; } = string.Empty;

        public decimal Revenue { get; set; }

        public int TicketsSold { get; set; }

        public int SeatsTotal { get; set; }

        public int TripCount { get; set; }

        public double OccupancyPercent => SeatsTotal > 0 ? Math.Round(TicketsSold * 100.0 / SeatsTotal, 1) : 0;
    }

    /// <summary>Doanh thu nhóm theo ngày.</summary>
    public class DailyRevenueItem
    {
        public DateTime Date { get; set; }

        public decimal Revenue { get; set; }

        public int TicketsSold { get; set; }
    }

    /// <summary>Dòng hiển thị tài khoản nhân sự trong bảng quản lý.</summary>
    public class StaffUserListItem
    {
        public string Id { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string FullName { get; set; } = string.Empty;

        public string? PhoneNumber { get; set; }

        public List<string> Roles { get; set; } = new();

        public DateTime CreatedAt { get; set; }

        public bool IsLockedOut { get; set; }

        public DateTimeOffset? LockoutEnd { get; set; }

        public int BookingCount { get; set; }

        public bool IsCurrentUser { get; set; }
    }

    /// <summary>Tạo mới tài khoản nhân sự (cấp quyền RouteManager / TicketInspector / Admin).</summary>
    public class StaffCreateViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên nhân sự.")]
        [StringLength(100)]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập email đăng nhập.")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
        [Display(Name = "Email đăng nhập")]
        public string Email { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Số điện thoại không đúng định dạng.")]
        [Display(Name = "Số điện thoại")]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự.")]
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
        [Display(Name = "Vai trò")]
        public string Role { get; set; } = "TicketInspector";
    }

    /// <summary>Chỉnh sửa thông tin & vai trò của tài khoản nhân sự.</summary>
    public class StaffEditViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập họ tên nhân sự.")]
        [StringLength(100)]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; } = string.Empty;

        [Phone(ErrorMessage = "Số điện thoại không đúng định dạng.")]
        [Display(Name = "Số điện thoại")]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn vai trò.")]
        [Display(Name = "Vai trò")]
        public string Role { get; set; } = "TicketInspector";

        public string Email { get; set; } = string.Empty;

        public List<string> CurrentRoles { get; set; } = new();

        public bool IsLockedOut { get; set; }
    }
}

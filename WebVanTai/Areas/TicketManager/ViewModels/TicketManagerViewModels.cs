using System.ComponentModel.DataAnnotations;
using WebVanTai.Models;
using WebVanTai.Models.Enums;
using WebVanTai.Models.ViewModels;

namespace WebVanTai.Areas.TicketManager.ViewModels
{
    /// <summary>Bảng điều khiển Quản lý vé: danh sách chuyến sắp chạy + KPI quầy hôm nay.</summary>
    public class ManagerDashboardViewModel
    {
        public DateTime SelectedDate { get; set; }
        public string? SearchRoute { get; set; }
        public decimal TodayCashRevenue { get; set; }
        public int TodayCounterTicketsSold { get; set; }
        public int PendingBookingsCount { get; set; }
        public List<ManagerTripItemViewModel> Trips { get; set; } = new();
    }

    public class ManagerTripItemViewModel
    {
        public int TripId { get; set; }
        public string RouteName { get; set; } = string.Empty;
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public string LicensePlate { get; set; } = string.Empty;
        public string BusTypeName { get; set; } = string.Empty;
        public string? DriverName { get; set; }
        public string? AssistantName { get; set; }
        public decimal BasePrice { get; set; }
        public int TotalSeats { get; set; }
        public int OccupiedSeats { get; set; }
        public int AvailableSeats => Math.Max(0, TotalSeats - OccupiedSeats);
        public TripStatus Status { get; set; }
    }

    /// <summary>Form bán vé tại quầy (thanh toán tiền mặt).</summary>
    public class ManagerSellViewModel
    {
        public int TripId { get; set; }
        public string RouteName { get; set; } = string.Empty;
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public decimal BasePrice { get; set; }
        public string BusTypeName { get; set; } = string.Empty;
        public string LicensePlate { get; set; } = string.Empty;

        public List<SeatDeckViewModel> Decks { get; set; } = new();
        public List<StationOptionViewModel> PickupStations { get; set; } = new();
        public List<StationOptionViewModel> DropoffStations { get; set; } = new();

        [Required(ErrorMessage = "Vui lòng chọn ít nhất 1 ghế.")]
        public string SelectedSeats { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn điểm đón.")]
        public int PickupStationId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn điểm trả.")]
        public int DropoffStationId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên hành khách.")]
        [StringLength(100)]
        [Display(Name = "Họ và tên hành khách")]
        public string PassengerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
        [StringLength(20)]
        [Display(Name = "Số điện thoại")]
        public string PassengerPhone { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [StringLength(150)]
        [Display(Name = "Email nhận vé điện tử (tùy chọn)")]
        public string? ContactEmail { get; set; }

        /// <summary>Tài khoản khách hàng liên kết (nếu khách có tài khoản G-Trans) — để vé hiện trong "Vé của tôi".</summary>
        public string? LinkedCustomerId { get; set; }
    }

    public class ManagerBookingListItemViewModel
    {
        public int BookingId { get; set; }
        public string RouteName { get; set; } = string.Empty;
        public DateTime DepartureTime { get; set; }
        public string LicensePlate { get; set; } = string.Empty;
        public string PassengerName { get; set; } = string.Empty;
        public string PassengerPhone { get; set; } = string.Empty;
        public string? CustomerEmail { get; set; }
        public string SeatCodes { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal RefundedAmount { get; set; }
        public BookingStatus Status { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiredAt { get; set; }
        public string? SoldByName { get; set; }
        public bool CanCollectCash => Status == BookingStatus.Pending && DateTime.UtcNow < ExpiredAt;
        public bool CanCancel => Status == BookingStatus.Paid;
    }

    /// <summary>Danh sách vé toàn hệ thống cho nhân sự Quản lý vé.</summary>
    public class ManagerTicketListItemViewModel
    {
        public int TicketId { get; set; }
        public int BookingId { get; set; }
        public string SeatCode { get; set; } = string.Empty;
        public string PassengerName { get; set; } = string.Empty;
        public string PassengerPhone { get; set; } = string.Empty;
        public string RouteName { get; set; } = string.Empty;
        public DateTime DepartureTime { get; set; }
        public string LicensePlate { get; set; } = string.Empty;
        public TicketStatus TicketStatus { get; set; }
        public BookingStatus BookingStatus { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool CanPrint => BookingStatus == BookingStatus.Paid;
    }

    public class ManagerCustomerLookupViewModel
    {
        public bool Found { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PhoneNumber { get; set; }
    }
}

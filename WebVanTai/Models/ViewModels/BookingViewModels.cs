using System.ComponentModel.DataAnnotations;
using WebVanTai.Models.Enums;

namespace WebVanTai.Models.ViewModels
{
    public class SeatItemViewModel
    {
        public string Code { get; set; } = string.Empty;
        public int Row { get; set; }
        public int Col { get; set; }
        public string Type { get; set; } = "Standard";
        public bool IsOccupied { get; set; }
    }

    public class SeatDeckViewModel
    {
        public int DeckNumber { get; set; }
        public string DeckName { get; set; } = string.Empty;
        public int Rows { get; set; }
        public int Cols { get; set; }
        public List<SeatItemViewModel> Seats { get; set; } = new();
    }

    public class StationOptionViewModel
    {
        public int StationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public int StopOrder { get; set; }
        public int OffsetMinutes { get; set; }
        public StationType StationType { get; set; }
    }

    public class TripBookingViewModel
    {
        public int TripId { get; set; }
        public string StartProvince { get; set; } = string.Empty;
        public string EndProvince { get; set; } = string.Empty;
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public decimal BasePrice { get; set; }
        public string BusTypeName { get; set; } = string.Empty;
        public string LicensePlate { get; set; } = string.Empty;
        public string Amenities { get; set; } = string.Empty;

        public List<SeatDeckViewModel> Decks { get; set; } = new();
        public List<StationOptionViewModel> PickupStations { get; set; } = new();
        public List<StationOptionViewModel> DropoffStations { get; set; } = new();

        // Form post inputs
        [Required(ErrorMessage = "Vui lòng chọn ít nhất 1 ghế")]
        public string SelectedSeats { get; set; } = string.Empty; // Comma-separated: "A01,A02"

        [Required(ErrorMessage = "Vui lòng chọn điểm đón")]
        [Display(Name = "Điểm đón")]
        public int PickupStationId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn điểm trả")]
        [Display(Name = "Điểm trả")]
        public int DropoffStationId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ và tên hành khách")]
        [StringLength(100, ErrorMessage = "Tên không quá 100 ký tự")]
        [Display(Name = "Họ và tên")]
        public string PassengerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [RegularExpression(@"^(0[3|5|7|8|9])+([0-9]{8})$", ErrorMessage = "Số điện thoại không hợp lệ (10 chữ số)")]
        [Display(Name = "Số điện thoại")]
        public string PassengerPhone { get; set; } = string.Empty;
    }

    public class BookingConfirmationViewModel
    {
        public int BookingId { get; set; }
        public BookingStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiredAt { get; set; }
        public decimal TotalAmount { get; set; }

        public int TripId { get; set; }
        public string RouteName { get; set; } = string.Empty;
        public DateTime DepartureTime { get; set; }
        public DateTime ArrivalTime { get; set; }
        public string BusTypeName { get; set; } = string.Empty;
        public string LicensePlate { get; set; } = string.Empty;

        public string PickupStationName { get; set; } = string.Empty;
        public string PickupStationAddress { get; set; } = string.Empty;
        public string DropoffStationName { get; set; } = string.Empty;
        public string DropoffStationAddress { get; set; } = string.Empty;

        public string PassengerName { get; set; } = string.Empty;
        public string PassengerPhone { get; set; } = string.Empty;
        public List<string> SeatCodes { get; set; } = new();
    }
}


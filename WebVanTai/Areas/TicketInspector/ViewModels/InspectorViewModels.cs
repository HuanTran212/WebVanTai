using WebVanTai.Models.Enums;

namespace WebVanTai.Areas.TicketInspector.ViewModels
{
    /// <summary>
    /// Một chuyến xe hiển thị trong danh sách "chọn chuyến xuất bến trong ngày" của nhân viên soát vé.
    /// </summary>
    public class InspectorTripListItem
    {
        public int TripId { get; set; }

        public string RouteName { get; set; } = string.Empty;

        public DateTime DepartureTime { get; set; }

        public DateTime ArrivalTime { get; set; }

        public string LicensePlate { get; set; } = string.Empty;

        public string BusTypeName { get; set; } = string.Empty;

        public string Amenities { get; set; } = string.Empty;

        public TripStatus Status { get; set; }

        /// <summary>Tổng số ghế vật lý của xe (BusType.TotalSeats).</summary>
        public int TotalSeats { get; set; }

        /// <summary>Số vé còn hiệu lực (Valid) - khách đã mua chưa lên xe.</summary>
        public int ValidTickets { get; set; }

        /// <summary>Số vé đã soát (CheckedIn) - khách đã lên xe.</summary>
        public int CheckedInTickets { get; set; }

        public int SoldTickets => ValidTickets + CheckedInTickets;

        public decimal BasePrice { get; set; }

        public double OccupancyPercent => TotalSeats > 0 ? Math.Round(SoldTickets * 100.0 / TotalSeats, 1) : 0;

        public double CheckedInPercent => SoldTickets > 0 ? Math.Round(CheckedInTickets * 100.0 / SoldTickets, 1) : 0;
    }

    /// <summary>
    /// ViewModel cho trang soát vé (camera quét QR + sơ đồ ghế thời gian thực).
    /// </summary>
    public class ScannerPageViewModel
    {
        public int TripId { get; set; }

        public string RouteName { get; set; } = string.Empty;

        public DateTime DepartureTime { get; set; }

        public DateTime ArrivalTime { get; set; }

        public string LicensePlate { get; set; } = string.Empty;

        public string BusTypeName { get; set; } = string.Empty;

        public int TotalSeats { get; set; }

        public int ValidTickets { get; set; }

        public int CheckedInTickets { get; set; }

        public TripStatus TripStatus { get; set; }

        public List<InspectorSeatDeckViewModel> Decks { get; set; } = new();
    }

    public class InspectorSeatDeckViewModel
    {
        public int DeckNumber { get; set; }

        public string DeckName { get; set; } = string.Empty;

        public int Rows { get; set; }

        public int Cols { get; set; }

        public List<InspectorSeatViewModel> Seats { get; set; } = new();
    }

    public class InspectorSeatViewModel
    {
        public string Code { get; set; } = string.Empty;

        public int Row { get; set; }

        public int Col { get; set; }

        /// <summary>available = chưa bán | sold = đã bán chưa lên xe | checkedin = đã lên xe.</summary>
        public string Status { get; set; } = "available";

        public string PassengerName { get; set; } = string.Empty;

        public string PassengerPhone { get; set; } = string.Empty;

        /// <summary>Vé đã bị vô hiệu (Void) - hiển thị gạch chéo.</summary>
        public bool IsVoid { get; set; }
    }

    /// <summary>Kết quả kiểm tra QrToken trả về cho client (thẻ thông báo xanh/đỏ).</summary>
    public class ScanResultViewModel
    {
        /// <summary>success | already | wrongtrip | void | notfound | unpaid | invalid.</summary>
        public string ResultType { get; set; } = "invalid";

        public bool IsSuccess => ResultType == "success";

        /// <summary>Tiêu đề thẻ thông báo (VD: "Soát vé thành công!").</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Mô tả chi tiết dưới tiêu đề.</summary>
        public string Message { get; set; } = string.Empty;

        public string PassengerName { get; set; } = string.Empty;

        public string SeatCode { get; set; } = string.Empty;

        public string PassengerPhone { get; set; } = string.Empty;

        public int TicketId { get; set; }

        public DateTime? CheckedInAtUtc { get; set; }

        /// <summary>Thông tin chuyến của vé (dùng khi quét nhầm chuyến khác).</summary>
        public string? TicketTripInfo { get; set; }

        public static ScanResultViewModel Invalid(string message) =>
            new() { ResultType = "invalid", Title = "Mã QR không hợp lệ", Message = message };
    }

    /// <summary>Dữ liệu sơ đồ ghế thời gian thực cho API SeatMap.</summary>
    public class SeatMapResponse
    {
        public int TotalSeats { get; set; }

        public int SoldTickets { get; set; }

        public int CheckedInTickets { get; set; }

        public int ValidTickets { get; set; }

        /// <summary>Trạng thái ghế theo mã ghế: { "A01": { status, passengerName, ... } }.</summary>
        public Dictionary<string, SeatStatusDto> Seats { get; set; } = new();
    }

    public class SeatStatusDto
    {
        public string Code { get; set; } = string.Empty;

        /// <summary>available | sold | checkedin | void.</summary>
        public string Status { get; set; } = "available";

        public string PassengerName { get; set; } = string.Empty;

        public string PassengerPhone { get; set; } = string.Empty;

        public int TicketId { get; set; }

        public string? CheckedInAt { get; set; }
    }
}

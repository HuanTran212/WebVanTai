using System;
using System.Collections.Generic;

namespace WebVanTai.Models.ViewModels
{
    public class ChatbotMessageDto
    {
        public string Role { get; set; } = "user"; // "user", "assistant", "system"
        public string Content { get; set; } = string.Empty;
    }

    public class ChatbotRequest
    {
        public string Message { get; set; } = string.Empty;
        public List<ChatbotMessageDto>? History { get; set; }
        public string? ApiKey { get; set; }
        public string? Model { get; set; }
        public string? BaseUrl { get; set; }
    }

    public class OpenAiConfigDto
    {
        public string ApiKey { get; set; } = string.Empty;
        public string? Model { get; set; }
        public string? BaseUrl { get; set; }
    }

    public class ChatbotResponse
    {
        public bool Success { get; set; } = true;
        public string Reply { get; set; } = string.Empty;
        public string Intent { get; set; } = "general"; // "search_trips", "lookup_ticket", "upcoming_trips", "statistics", "general"
        public bool IsOpenAi { get; set; }
        public string? Model { get; set; }
        public object? Data { get; set; }
        public List<string> Suggestions { get; set; } = new();
    }

    public class TripCardDto
    {
        public int TripId { get; set; }
        public string RouteName { get; set; } = string.Empty;
        public string StartProvince { get; set; } = string.Empty;
        public string EndProvince { get; set; } = string.Empty;
        public string DepartureTimeStr { get; set; } = string.Empty;
        public string ArrivalTimeStr { get; set; } = string.Empty;
        public decimal BasePrice { get; set; }
        public string FormattedPrice { get; set; } = string.Empty;
        public string BusTypeName { get; set; } = string.Empty;
        public string LicensePlate { get; set; } = string.Empty;
        public int AvailableSeats { get; set; }
        public int TotalSeats { get; set; }
        public string BookingUrl { get; set; } = string.Empty;
        public string Status { get; set; } = "Scheduled";
        public string TimeRemaining { get; set; } = string.Empty;
    }

    public class TicketCardDto
    {
        public int TicketId { get; set; }
        public int BookingId { get; set; }
        public string SeatCode { get; set; } = string.Empty;
        public string PassengerName { get; set; } = string.Empty;
        public string PassengerPhone { get; set; } = string.Empty;
        public string RouteName { get; set; } = string.Empty;
        public string DepartureTimeStr { get; set; } = string.Empty;
        public string LicensePlate { get; set; } = string.Empty;
        public string Status { get; set; } = "Valid";
        public string StatusBadgeClass { get; set; } = "bg-success";
        public string StatusDescription { get; set; } = "Hợp lệ";
        public decimal TotalAmount { get; set; }
        public string FormattedAmount { get; set; } = string.Empty;
        public string? QrToken { get; set; }
        public string? PrintTicketUrl { get; set; }
        public string PickupStationName { get; set; } = string.Empty;
        public string DropoffStationName { get; set; } = string.Empty;
    }

    public class StaffStatsDto
    {
        public string PeriodTitle { get; set; } = "Hôm nay";
        public decimal TotalRevenue { get; set; }
        public string FormattedRevenue { get; set; } = "0 đ";
        public int TotalTicketsSold { get; set; }
        public int TotalPaidBookings { get; set; }
        public int TotalTripsRunning { get; set; }
        public double AverageOccupancyRate { get; set; }
        public List<RouteStatItemDto> TopRoutes { get; set; } = new();
    }

    public class RouteStatItemDto
    {
        public string RouteName { get; set; } = string.Empty;
        public int TicketsSold { get; set; }
        public decimal Revenue { get; set; }
        public string FormattedRevenue { get; set; } = "0 đ";
    }
}

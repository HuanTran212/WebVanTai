using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WebVanTai.Data;
using WebVanTai.Models.Enums;
using WebVanTai.Areas.TicketInspector.ViewModels;

namespace WebVanTai.Areas.TicketInspector.Controllers
{
    [Area("TicketInspector")]
    [Authorize(Roles = "TicketInspector")]
    public class ScannerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ScannerController> _logger;

        public ScannerController(ApplicationDbContext context, ILogger<ScannerController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Trang soát vé mobile: mở camera quét mã QR của khách (html5-qrcode),
        /// hiển thị thẻ thông báo kết quả và sơ đồ ghế cập nhật thời gian thực.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(int tripId)
        {
            var trip = await _context.Trips
                .AsNoTracking()
                .Include(t => t.Route)
                .Include(t => t.Bus)
                    .ThenInclude(b => b!.BusType)
                .Include(t => t.Tickets)
                .FirstOrDefaultAsync(t => t.TripId == tripId);

            if (trip == null || trip.Bus?.BusType == null || trip.Route == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy chuyến xe cần soát vé.";
                return RedirectToAction(nameof(DashboardController.Index), "Dashboard");
            }

            var activeTickets = trip.Tickets.Where(x => x.Status != TicketStatus.Void).ToList();

            var model = new ScannerPageViewModel
            {
                TripId = trip.TripId,
                RouteName = $"{trip.Route.StartProvince} → {trip.Route.EndProvince}",
                DepartureTime = trip.DepartureTime,
                ArrivalTime = trip.ArrivalTime,
                LicensePlate = trip.Bus.LicensePlate,
                BusTypeName = trip.Bus.BusType.Name,
                TotalSeats = trip.Bus.BusType.TotalSeats,
                ValidTickets = activeTickets.Count(x => x.Status == TicketStatus.Valid),
                CheckedInTickets = activeTickets.Count(x => x.Status == TicketStatus.CheckedIn),
                TripStatus = trip.Status
            };

            // Parse sơ đồ ghế 2 tầng từ SeatLayoutJson (cùng cấu trúc với luồng đặt vé)
            if (!string.IsNullOrWhiteSpace(trip.Bus.BusType.SeatLayoutJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(trip.Bus.BusType.SeatLayoutJson);
                    if (doc.RootElement.TryGetProperty("decks", out var decksElement) && decksElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var deckJson in decksElement.EnumerateArray())
                        {
                            var deckVm = new InspectorSeatDeckViewModel
                            {
                                DeckNumber = deckJson.TryGetProperty("deckNumber", out var dn) ? dn.GetInt32() : 1,
                                DeckName = deckJson.TryGetProperty("deckName", out var dName) ? dName.GetString() ?? "Tầng" : "Tầng",
                                Rows = deckJson.TryGetProperty("rows", out var rw) ? rw.GetInt32() : 6,
                                Cols = deckJson.TryGetProperty("cols", out var cl) ? cl.GetInt32() : 3
                            };

                            if (deckJson.TryGetProperty("seats", out var seatsJson) && seatsJson.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var seat in seatsJson.EnumerateArray())
                                {
                                    var code = seat.TryGetProperty("code", out var cd) ? cd.GetString() ?? "" : "";
                                    deckVm.Seats.Add(new InspectorSeatViewModel
                                    {
                                        Code = code,
                                        Row = seat.TryGetProperty("row", out var r) ? r.GetInt32() : 1,
                                        Col = seat.TryGetProperty("col", out var c) ? c.GetInt32() : 1
                                    });
                                }
                            }

                            model.Decks.Add(deckVm);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi parse SeatLayoutJson khi mở trang soát vé chuyến #{TripId}", tripId);
                }
            }

            return View(model);
        }
    }
}

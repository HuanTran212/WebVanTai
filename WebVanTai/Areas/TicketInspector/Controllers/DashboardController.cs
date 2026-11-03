using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebVanTai.Data;
using WebVanTai.Models.Enums;
using WebVanTai.Areas.TicketInspector.ViewModels;

namespace WebVanTai.Areas.TicketInspector.Controllers
{
    [Area("TicketInspector")]
    [Authorize(Roles = "TicketInspector")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(ApplicationDbContext context, ILogger<DashboardController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Danh sách chuyến xe xuất bến trong ngày (mặc định hôm nay) cho nhân viên soát vé chọn.
        /// Hỗ trợ điều hướng theo ngày qua query ?date=yyyy-MM-dd.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(DateTime? date)
        {
            // Chạy trên giờ máy chủ (khớp với DbInitializer seed theo DateTime.Today)
            var selectedDate = (date ?? DateTime.Today).Date;
            if (selectedDate > DateTime.Today.AddDays(30))
            {
                selectedDate = DateTime.Today.AddDays(30);
            }

            var nextDay = selectedDate.AddDays(1);

            var trips = await _context.Trips
                .AsNoTracking()
                .Include(t => t.Route)
                .Include(t => t.Bus)
                    .ThenInclude(b => b!.BusType)
                .Include(t => t.Tickets)
                .Where(t => t.DepartureTime >= selectedDate && t.DepartureTime < nextDay)
                .OrderBy(t => t.DepartureTime)
                .ToListAsync();

            var items = trips.Select(t =>
            {
                var activeTickets = t.Tickets.Where(x => x.Status != TicketStatus.Void).ToList();
                return new InspectorTripListItem
                {
                    TripId = t.TripId,
                    RouteName = t.Route == null
                        ? "Tuyến không xác định"
                        : $"{t.Route.StartProvince} → {t.Route.EndProvince}",
                    DepartureTime = t.DepartureTime,
                    ArrivalTime = t.ArrivalTime,
                    LicensePlate = t.Bus?.LicensePlate ?? "N/A",
                    BusTypeName = t.Bus?.BusType?.Name ?? "Xe khách",
                    Amenities = t.Bus?.Amenities ?? string.Empty,
                    Status = t.Status,
                    TotalSeats = t.Bus?.BusType?.TotalSeats ?? 0,
                    ValidTickets = activeTickets.Count(x => x.Status == TicketStatus.Valid),
                    CheckedInTickets = activeTickets.Count(x => x.Status == TicketStatus.CheckedIn),
                    BasePrice = t.BasePrice
                };
            }).ToList();

            ViewBag.SelectedDate = selectedDate;
            ViewBag.IsToday = selectedDate == DateTime.Today;

            return View(items);
        }
    }
}

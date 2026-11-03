using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebVanTai.Data;
using WebVanTai.Models.Enums;
using WebVanTai.Areas.Admin.ViewModels;

namespace WebVanTai.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
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
        /// Bảng điều khiển thống kê doanh thu theo tuyến & tỷ lệ lấp đầy ghế (Chart.js).
        /// Lọc khoảng thời gian bằng query ?from=yyyy-MM-dd&to=yyyy-MM-dd, mặc định tháng hiện tại.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(DateTime? from, DateTime? to)
        {
            var today = DateTime.Today;
            var fromDate = (from ?? new DateTime(today.Year, today.Month, 1)).Date;
            var toDate = (to ?? today).Date;

            if (fromDate > toDate)
            {
                (fromDate, toDate) = (toDate, fromDate);
            }
            if ((toDate - fromDate).TotalDays > 366)
            {
                fromDate = toDate.AddDays(-366);
            }

            var rangeStart = fromDate;
            var rangeEnd = toDate.AddDays(1); // vượt biên để lấy trọn ngày cuối

            var model = new RevenueDashboardViewModel
            {
                FromDate = fromDate,
                ToDate = toDate
            };

            // ===== 1. Doanh thu từ các đặt chỗ đã thanh toán (Paid) trong khoảng =====
            var paidBookings = await _context.Bookings
                .AsNoTracking()
                .Where(b => b.Status == BookingStatus.Paid && b.CreatedAt >= rangeStart && b.CreatedAt < rangeEnd)
                .Select(b => new { b.TotalAmount, b.CreatedAt, b.TripId })
                .ToListAsync();

            model.TotalRevenue = paidBookings.Sum(b => b.TotalAmount);
            model.TotalPaidBookings = paidBookings.Count;

            // Doanh thu theo tuyến (join Trip để lấy RouteId)
            var tripRoutePairs = await _context.Trips
                .AsNoTracking()
                .Select(t => new { t.TripId, t.RouteId })
                .ToDictionaryAsync(t => t.TripId, t => t.RouteId);

            var revenuePerRoute = paidBookings
                .GroupBy(b => tripRoutePairs.TryGetValue(b.TripId, out var rid) ? rid : 0)
                .Select(g => new RouteRevenueItem { RouteId = g.Key, Revenue = g.Sum(x => x.TotalAmount) })
                .ToDictionary(x => x.RouteId, x => x);

            // ===== 2. Vé bán ra & đã soát trong khoảng =====
            var ticketStats = await _context.Tickets
                .AsNoTracking()
                .Where(t => t.Booking!.Status == BookingStatus.Paid
                         && t.Booking.CreatedAt >= rangeStart && t.Booking.CreatedAt < rangeEnd
                         && t.Status != TicketStatus.Void)
                .Select(t => new { t.Status, t.TripId })
                .ToListAsync();

            model.TotalTicketsSold = ticketStats.Count;
            model.TotalCheckedIn = ticketStats.Count(t => t.Status == TicketStatus.CheckedIn);

            // Ghế bán theo tuyến (cho tỷ lệ lấp đầy)
            var soldPerRoute = ticketStats
                .GroupBy(t => tripRoutePairs.TryGetValue(t.TripId, out var rid) ? rid : 0)
                .ToDictionary(g => g.Key, g => g.Count());

            // ===== 3. Công suất xe theo tuyến (chuyến khởi hành trong khoảng, trừ chuyến hủy) =====
            var tripsInRange = await _context.Trips
                .AsNoTracking()
                .Include(t => t.Route)
                .Include(t => t.Bus)
                    .ThenInclude(b => b!.BusType)
                .Where(t => t.DepartureTime >= rangeStart && t.DepartureTime < rangeEnd && t.Status != TripStatus.Cancelled)
                .ToListAsync();

            var routeItems = new List<RouteRevenueItem>();
            foreach (var group in tripsInRange.GroupBy(t => t.RouteId))
            {
                var route = group.First().Route;
                var item = new RouteRevenueItem
                {
                    RouteId = group.Key,
                    RouteName = route == null ? $"Tuyến #{group.Key}" : $"{route.StartProvince} → {route.EndProvince}",
                    TripCount = group.Count(),
                    SeatsTotal = group.Sum(t => t.Bus?.BusType?.TotalSeats ?? 0)
                };
                if (soldPerRoute.TryGetValue(group.Key, out var sold))
                {
                    item.TicketsSold = sold;
                }
                if (revenuePerRoute.TryGetValue(group.Key, out var rev))
                {
                    item.Revenue = rev.Revenue;
                }
                routeItems.Add(item);
            }

            // Tuyến có doanh thu nhưng chưa có chuyến trong khoảng (đề phòng lệch ngày tạo vé / ngày chạy)
            foreach (var kv in revenuePerRoute)
            {
                if (!routeItems.Any(r => r.RouteId == kv.Key))
                {
                    var routeName = await _context.Routes
                        .Where(r => r.RouteId == kv.Key)
                        .Select(r => r.StartProvince + " → " + r.EndProvince)
                        .FirstOrDefaultAsync();
                    routeItems.Add(new RouteRevenueItem
                    {
                        RouteId = kv.Key,
                        RouteName = routeName ?? $"Tuyến #{kv.Key}",
                        Revenue = kv.Value.Revenue
                    });
                }
            }

            model.RouteRevenues = routeItems
                .OrderByDescending(r => r.Revenue)
                .ThenByDescending(r => r.TicketsSold)
                .ToList();

            model.TotalTripsInRange = tripsInRange.Count;
            model.TotalSeatsAvailable = model.RouteRevenues.Sum(r => r.SeatsTotal);
            model.OverallOccupancyPercent = model.TotalSeatsAvailable > 0
                ? Math.Round(model.TotalTicketsSold * 100.0 / model.TotalSeatsAvailable, 1)
                : 0;

            // ===== 4. Doanh thu nhóm theo ngày (biểu đồ đường) =====
            model.DailyRevenues = paidBookings
                .GroupBy(b => b.CreatedAt.Date)
                .Select(g => new DailyRevenueItem
                {
                    Date = g.Key,
                    Revenue = g.Sum(x => x.TotalAmount),
                    TicketsSold = g.Count()
                })
                .OrderBy(d => d.Date)
                .ToList();

            return View(model);
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebVanTai.Areas.RouteManager.Models;
using WebVanTai.Data;
using WebVanTai.Models.Enums;

namespace WebVanTai.Areas.RouteManager.Controllers
{
    [Area("RouteManager")]
    [Authorize(Roles = "RouteManager,Admin")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var vm = new DashboardViewModel
            {
                TotalStations = await _context.Stations.CountAsync(),
                TotalRoutes = await _context.Routes.CountAsync(),
                TotalBusTypes = await _context.BusTypes.CountAsync(),
                TotalBuses = await _context.Buses.CountAsync(),
                TotalTrips = await _context.Trips.CountAsync(),
                ScheduledTrips = await _context.Trips.CountAsync(t => t.Status == TripStatus.Scheduled),
                RunningTrips = await _context.Trips.CountAsync(t => t.Status == TripStatus.Running),
                RecentTrips = await _context.Trips
                    .Include(t => t.Route)
                    .Include(t => t.Bus)
                    .OrderByDescending(t => t.DepartureTime)
                    .Take(5)
                    .ToListAsync()
            };

            return View(vm);
        }
    }
}

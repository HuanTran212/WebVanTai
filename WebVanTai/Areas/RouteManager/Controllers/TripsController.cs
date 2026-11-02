using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebVanTai.Data;
using WebVanTai.Models;
using WebVanTai.Models.Enums;
using Route = WebVanTai.Models.Route;

namespace WebVanTai.Areas.RouteManager.Controllers
{
    [Area("RouteManager")]
    [Authorize(Roles = "RouteManager,Admin")]
    public class TripsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public TripsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: RouteManager/Trips
        public async Task<IActionResult> Index(int? routeId, int? busId, TripStatus? status, DateTime? departureDate)
        {
            var query = _context.Trips
                .Include(t => t.Route)
                .Include(t => t.Bus)
                    .ThenInclude(b => b!.BusType)
                .Include(t => t.Bookings)
                .Include(t => t.Tickets)
                .AsQueryable();

            if (routeId.HasValue)
            {
                query = query.Where(t => t.RouteId == routeId.Value);
            }

            if (busId.HasValue)
            {
                query = query.Where(t => t.BusId == busId.Value);
            }

            if (status.HasValue)
            {
                query = query.Where(t => t.Status == status.Value);
            }

            if (departureDate.HasValue)
            {
                var start = departureDate.Value.Date;
                var end = start.AddDays(1);
                query = query.Where(t => t.DepartureTime >= start && t.DepartureTime < end);
            }

            ViewBag.Routes = new SelectList(await _context.Routes.OrderBy(r => r.StartProvince).Select(r => new { r.RouteId, Name = $"{r.StartProvince} - {r.EndProvince}" }).ToListAsync(), "RouteId", "Name", routeId);
            ViewBag.Buses = new SelectList(await _context.Buses.OrderBy(b => b.LicensePlate).Select(b => new { b.BusId, b.LicensePlate }).ToListAsync(), "BusId", "LicensePlate", busId);
            ViewBag.SelectedRouteId = routeId;
            ViewBag.SelectedBusId = busId;
            ViewBag.SelectedStatus = status;
            ViewBag.SelectedDate = departureDate?.ToString("yyyy-MM-dd");

            var trips = await query
                .OrderByDescending(t => t.DepartureTime)
                .ToListAsync();

            return View(trips);
        }

        // GET: RouteManager/Trips/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var trip = await _context.Trips
                .Include(t => t.Route)
                    .ThenInclude(r => r!.RouteStations.OrderBy(rs => rs.StopOrder))
                        .ThenInclude(rs => rs.Station)
                .Include(t => t.Bus)
                    .ThenInclude(b => b!.BusType)
                .Include(t => t.Bookings)
                    .ThenInclude(b => b.User)
                .Include(t => t.Tickets)
                .FirstOrDefaultAsync(m => m.TripId == id);

            if (trip == null) return NotFound();

            return View(trip);
        }

        // GET: RouteManager/Trips/Create
        public async Task<IActionResult> Create()
        {
            await PopulateDropdowns();
            var tomorrow = DateTime.Today.AddDays(1);
            var model = new Trip
            {
                DepartureTime = tomorrow.AddHours(7),
                ArrivalTime = tomorrow.AddHours(14),
                BasePrice = 250000m,
                Status = TripStatus.Scheduled
            };
            return View(model);
        }

        // POST: RouteManager/Trips/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TripId,RouteId,BusId,DepartureTime,ArrivalTime,BasePrice,Status")] Trip trip)
        {
            // Business Rule 1: ArrivalTime must be strictly after DepartureTime
            if (trip.ArrivalTime <= trip.DepartureTime)
            {
                ModelState.AddModelError("ArrivalTime", "Thời gian đến nơi (ArrivalTime) phải sau thời gian khởi hành (DepartureTime)!");
            }

            // Business Rule 2: Overlap check for BusId
            // A bus cannot be assigned to two trips with overlapping time intervals
            var conflict = await CheckBusScheduleConflictAsync(trip.BusId, trip.TripId, trip.DepartureTime, trip.ArrivalTime);
            if (conflict != null)
            {
                var errorMsg = $"Xung đột lịch trình: Xe biển số [{conflict.Bus?.LicensePlate}] đã được xếp cho Chuyến #{conflict.TripId} " +
                               $"({conflict.Route?.StartProvince} → {conflict.Route?.EndProvince}) " +
                               $"từ {conflict.DepartureTime:HH:mm dd/MM/yyyy} đến {conflict.ArrivalTime:HH:mm dd/MM/yyyy}. " +
                               $"Không thể xếp trùng giờ!";
                ModelState.AddModelError("BusId", errorMsg);
                ModelState.AddModelError(string.Empty, errorMsg);
            }

            if (ModelState.IsValid)
            {
                _context.Add(trip);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã lên lịch chuyến xe #{trip.TripId} thành công!";
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropdowns(trip.RouteId, trip.BusId);
            return View(trip);
        }

        // GET: RouteManager/Trips/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var trip = await _context.Trips.FindAsync(id);
            if (trip == null) return NotFound();

            await PopulateDropdowns(trip.RouteId, trip.BusId);
            return View(trip);
        }

        // POST: RouteManager/Trips/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("TripId,RouteId,BusId,DepartureTime,ArrivalTime,BasePrice,Status")] Trip trip)
        {
            if (id != trip.TripId) return NotFound();

            // Business Rule 1: ArrivalTime > DepartureTime
            if (trip.ArrivalTime <= trip.DepartureTime)
            {
                ModelState.AddModelError("ArrivalTime", "Thời gian đến nơi (ArrivalTime) phải sau thời gian khởi hành (DepartureTime)!");
            }

            // Business Rule 2: Overlap check for BusId excluding current trip
            var conflict = await CheckBusScheduleConflictAsync(trip.BusId, trip.TripId, trip.DepartureTime, trip.ArrivalTime);
            if (conflict != null)
            {
                var errorMsg = $"Xung đột lịch trình: Xe biển số [{conflict.Bus?.LicensePlate}] đã được xếp cho Chuyến #{conflict.TripId} " +
                               $"({conflict.Route?.StartProvince} → {conflict.Route?.EndProvince}) " +
                               $"từ {conflict.DepartureTime:HH:mm dd/MM/yyyy} đến {conflict.ArrivalTime:HH:mm dd/MM/yyyy}. " +
                               $"Không thể xếp trùng giờ!";
                ModelState.AddModelError("BusId", errorMsg);
                ModelState.AddModelError(string.Empty, errorMsg);
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(trip);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Đã cập nhật chuyến xe #{trip.TripId} thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TripExists(trip.TripId))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropdowns(trip.RouteId, trip.BusId);
            return View(trip);
        }

        // GET: RouteManager/Trips/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var trip = await _context.Trips
                .Include(t => t.Route)
                .Include(t => t.Bus)
                .Include(t => t.Bookings)
                .Include(t => t.Tickets)
                .FirstOrDefaultAsync(m => m.TripId == id);

            if (trip == null) return NotFound();

            ViewBag.BookingsCount = trip.Bookings.Count;
            ViewBag.TicketsCount = trip.Tickets.Count;
            return View(trip);
        }

        // POST: RouteManager/Trips/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var trip = await _context.Trips
                .Include(t => t.Bookings)
                .Include(t => t.Tickets)
                .FirstOrDefaultAsync(m => m.TripId == id);

            if (trip == null) return NotFound();

            // Safety check: if trip already has paid bookings or tickets, prevent raw delete to protect customer data
            if (trip.Bookings.Any(b => b.Status == BookingStatus.Paid) || trip.Tickets.Any(t => t.Status == TicketStatus.Valid || t.Status == TicketStatus.CheckedIn))
            {
                TempData["ErrorMessage"] = $"Không thể xóa chuyến xe #{trip.TripId} vì đã có vé đã thanh toán! Hãy chuyển trạng thái chuyến sang 'Đã hủy' (Cancelled) để đảm bảo lịch sử.";
                return RedirectToAction(nameof(Index));
            }

            _context.Trips.Remove(trip);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã xóa chuyến xe #{id} thành công!";
            return RedirectToAction(nameof(Index));
        }

        // AJAX API: Get route duration to auto-calculate ArrivalTime
        [HttpGet]
        public async Task<IActionResult> GetRouteDuration(int routeId)
        {
            var route = await _context.Routes.FindAsync(routeId);
            if (route == null) return NotFound();

            return Json(new { durationHours = route.DurationHours, distanceKm = route.DistanceKm });
        }

        // Conflict check helper method
        private async Task<Trip?> CheckBusScheduleConflictAsync(int busId, int currentTripId, DateTime departure, DateTime arrival)
        {
            return await _context.Trips
                .Include(t => t.Bus)
                .Include(t => t.Route)
                .Where(t => t.BusId == busId
                         && t.TripId != currentTripId
                         && t.Status != TripStatus.Cancelled
                         && t.DepartureTime < arrival
                         && t.ArrivalTime > departure)
                .FirstOrDefaultAsync();
        }

        private async Task PopulateDropdowns(int? selectedRouteId = null, int? selectedBusId = null)
        {
            var routes = await _context.Routes
                .Where(r => r.IsActive)
                .OrderBy(r => r.StartProvince)
                .ThenBy(r => r.EndProvince)
                .Select(r => new
                {
                    r.RouteId,
                    DisplayText = $"{r.StartProvince} → {r.EndProvince} ({r.DistanceKm}km - {r.DurationHours}h)"
                })
                .ToListAsync();

            var buses = await _context.Buses
                .Where(b => b.IsActive)
                .Include(b => b.BusType)
                .OrderBy(b => b.LicensePlate)
                .Select(b => new
                {
                    b.BusId,
                    DisplayText = $"{b.LicensePlate} - {b.BusType!.Name} ({b.BusType.TotalSeats} chỗ)"
                })
                .ToListAsync();

            ViewBag.RouteId = new SelectList(routes, "RouteId", "DisplayText", selectedRouteId);
            ViewBag.BusId = new SelectList(buses, "BusId", "DisplayText", selectedBusId);
        }

        private bool TripExists(int id)
        {
            return _context.Trips.Any(e => e.TripId == id);
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebVanTai.Areas.RouteManager.Models;
using WebVanTai.Data;
using WebVanTai.Models;
using WebVanTai.Models.Enums;
using WebVanTai.Models.ViewModels;
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
        public async Task<IActionResult> Index(int? routeId, int? busId, TripStatus? status, DateTime? departureDate, int page = 1)
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

            var totalItems = await query.CountAsync();
            int pageSize = 15;
            page = Math.Max(1, page);

            var items = await query
                .OrderByDescending(t => t.DepartureTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var paginatedList = new PaginatedList<Trip>(items, totalItems, page, pageSize);
            return View(paginatedList);
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
            var model = new TripCreateViewModel
            {
                DepartureTime = tomorrow.AddHours(7),
                ArrivalTime = tomorrow.AddHours(14),
                BasePrice = 250000m,
                Status = TripStatus.Scheduled,
                IsDailyRecurring = false,
                RepeatEndDate = tomorrow.AddDays(6)
            };
            return View(model);
        }

        // POST: RouteManager/Trips/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TripCreateViewModel model)
        {
            // Business Rule: DepartureTime must be in the future
            if (model.DepartureTime <= WebVanTai.Services.VietnamTimeHelper.Now)
            {
                ModelState.AddModelError(nameof(model.DepartureTime), "Thời gian khởi hành (DepartureTime) phải ở trong tương lai!");
            }

            // Business Rule 1: ArrivalTime must be strictly after DepartureTime
            if (model.ArrivalTime <= model.DepartureTime)
            {
                ModelState.AddModelError(nameof(model.ArrivalTime), "Thời gian đến nơi (ArrivalTime) phải sau thời gian khởi hành (DepartureTime)!");
            }

            if (model.IsDailyRecurring)
            {
                if (!model.RepeatEndDate.HasValue)
                {
                    ModelState.AddModelError(nameof(model.RepeatEndDate), "Vui lòng chọn ngày kết thúc lặp lại.");
                }
                else if (model.RepeatEndDate.Value.Date < model.DepartureTime.Date)
                {
                    ModelState.AddModelError(nameof(model.RepeatEndDate), "Ngày kết thúc lặp lại phải lớn hơn hoặc bằng ngày khởi hành đầu tiên.");
                }
                else if ((model.RepeatEndDate.Value.Date - model.DepartureTime.Date).TotalDays > 90)
                {
                    ModelState.AddModelError(nameof(model.RepeatEndDate), "Chỉ hỗ trợ tạo lịch lặp lại hàng ngày tối đa 90 ngày một lần.");
                }
            }

            var acceptedSlots = new List<(DateTime Dep, DateTime Arr)>();
            var conflictMessages = new List<string>();

            if (ModelState.IsValid)
            {
                // Sinh các slot chuyến xe: 1 chuyến đơn hoặc lặp lại hàng ngày theo khung giờ
                var duration = model.ArrivalTime - model.DepartureTime;
                var startDate = model.DepartureTime.Date;
                var endDate = model.IsDailyRecurring && model.RepeatEndDate.HasValue
                    ? model.RepeatEndDate.Value.Date
                    : startDate;

                for (var d = startDate; d <= endDate; d = d.AddDays(1))
                {
                    var dep = d.Add(model.DepartureTime.TimeOfDay);
                    var arr = dep.Add(duration);

                    var dbConflict = await CheckBusScheduleConflictAsync(model.BusId, 0, dep, arr);
                    var batchOverlap = acceptedSlots.Any(s => s.Dep < arr && s.Arr > dep);

                    if (dbConflict != null)
                    {
                        conflictMessages.Add(
                            $"Ngày {dep:dd/MM/yyyy}: Xe [{dbConflict.Bus?.LicensePlate}] trùng với Chuyến #{dbConflict.TripId} " +
                            $"({dbConflict.DepartureTime:HH:mm dd/MM} - {dbConflict.ArrivalTime:HH:mm dd/MM}).");
                    }
                    else if (batchOverlap)
                    {
                        conflictMessages.Add($"Ngày {dep:dd/MM/yyyy}: Thời lượng chuyến vượt quá 24 giờ nên chồng lấn với chuyến ngày hôm trước.");
                    }
                    else
                    {
                        acceptedSlots.Add((dep, arr));
                    }
                }

                if (conflictMessages.Count > 0 && !model.SkipConflictingDates)
                {
                    foreach (var msg in conflictMessages.Take(5))
                    {
                        ModelState.AddModelError(string.Empty, msg);
                    }
                    if (conflictMessages.Count > 5)
                    {
                        ModelState.AddModelError(string.Empty, $"...và {conflictMessages.Count - 5} ngày xung đột khác.");
                    }
                    ModelState.AddModelError(nameof(model.BusId), "Phát hiện xung đột lịch xe trong khoảng thời gian đã chọn.");
                }
                else if (acceptedSlots.Count == 0)
                {
                    ModelState.AddModelError(string.Empty, "Tất cả các ngày trong khoảng lặp lại đều bị trùng lịch xe, không thể tạo chuyến mới.");
                }
            }

            if (ModelState.IsValid && acceptedSlots.Count > 0)
            {
                var trips = acceptedSlots.Select(slot => new Trip
                {
                    RouteId = model.RouteId,
                    BusId = model.BusId,
                    DepartureTime = slot.Dep,
                    ArrivalTime = slot.Arr,
                    BasePrice = model.BasePrice,
                    Status = model.Status
                }).ToList();

                await _context.Trips.AddRangeAsync(trips);
                await _context.SaveChangesAsync();

                if (trips.Count == 1 && !model.IsDailyRecurring)
                {
                    TempData["SuccessMessage"] = $"Đã lên lịch chuyến xe #{trips[0].TripId} thành công!";
                }
                else
                {
                    var skippedNote = conflictMessages.Count > 0
                        ? $" (đã bỏ qua {conflictMessages.Count} ngày trùng lịch)"
                        : string.Empty;
                    TempData["SuccessMessage"] =
                        $"Đã tạo thành công {trips.Count} chuyến xe chạy hàng ngày " +
                        $"({trips.First().DepartureTime:dd/MM/yyyy} – {trips.Last().DepartureTime:dd/MM/yyyy}){skippedNote}.";
                }
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropdowns(model.RouteId, model.BusId);
            return View(model);
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

            // Business Rule: DepartureTime must not be in the past for Scheduled trips
            if (trip.DepartureTime <= WebVanTai.Services.VietnamTimeHelper.Now && trip.Status == TripStatus.Scheduled)
            {
                ModelState.AddModelError("DepartureTime", "Không thể chỉnh thời gian khởi hành về quá khứ cho chuyến xe đang lên lịch!");
            }

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

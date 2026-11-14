using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebVanTai.Data;
using WebVanTai.Models;

using WebVanTai.Models.ViewModels;

namespace WebVanTai.Areas.RouteManager.Controllers
{
    [Area("RouteManager")]
    [Authorize(Roles = "RouteManager,Admin")]
    public class StationsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: RouteManager/Stations
        public async Task<IActionResult> Index(string? searchString, string? province, int page = 1)
        {
            var query = _context.Stations
                .Include(s => s.RouteStations)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                var search = searchString.Trim();
                query = query.Where(s => s.Name.Contains(search) || s.Address.Contains(search));
            }

            if (!string.IsNullOrWhiteSpace(province))
            {
                query = query.Where(s => s.Province == province);
            }

            ViewBag.Provinces = await _context.Stations
                .Select(s => s.Province)
                .Distinct()
                .OrderBy(p => p)
                .ToListAsync();

            ViewBag.CurrentSearch = searchString;
            ViewBag.CurrentProvince = province;

            var totalItems = await query.CountAsync();
            int pageSize = 15;
            page = Math.Max(1, page);

            var items = await query
                .OrderBy(s => s.Province)
                .ThenBy(s => s.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var paginatedList = new PaginatedList<Station>(items, totalItems, page, pageSize);
            return View(paginatedList);
        }

        // GET: RouteManager/Stations/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var station = await _context.Stations
                .Include(s => s.RouteStations)
                    .ThenInclude(rs => rs.Route)
                .FirstOrDefaultAsync(m => m.StationId == id);

            if (station == null) return NotFound();

            return View(station);
        }

        // GET: RouteManager/Stations/Create
        public IActionResult Create()
        {
            var model = new Station
            {
                Latitude = 10.776889m,
                Longitude = 106.700806m
            };
            return View(model);
        }

        // POST: RouteManager/Stations/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("StationId,Name,Province,Address,Latitude,Longitude")] Station station)
        {
            if (ModelState.IsValid)
            {
                _context.Add(station);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã thêm trạm dừng \"{station.Name}\" thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(station);
        }

        // GET: RouteManager/Stations/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var station = await _context.Stations.FindAsync(id);
            if (station == null) return NotFound();

            return View(station);
        }

        // POST: RouteManager/Stations/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("StationId,Name,Province,Address,Latitude,Longitude")] Station station)
        {
            if (id != station.StationId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(station);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Đã cập nhật thông tin trạm \"{station.Name}\" thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!StationExists(station.StationId))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(station);
        }

        // GET: RouteManager/Stations/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var station = await _context.Stations
                .Include(s => s.RouteStations)
                    .ThenInclude(rs => rs.Route)
                .FirstOrDefaultAsync(m => m.StationId == id);

            if (station == null) return NotFound();

            ViewBag.InUseRoutesCount = station.RouteStations.Count;
            ViewBag.InUseBookingsCount = await _context.Bookings
                .CountAsync(b => b.PickupStationId == id || b.DropoffStationId == id);

            return View(station);
        }

        // POST: RouteManager/Stations/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var station = await _context.Stations.FindAsync(id);
            if (station == null) return NotFound();

            var routeCount = await _context.RouteStations.CountAsync(rs => rs.StationId == id);
            var bookingCount = await _context.Bookings.CountAsync(b => b.PickupStationId == id || b.DropoffStationId == id);

            if (routeCount > 0 || bookingCount > 0)
            {
                TempData["ErrorMessage"] = $"Không thể xóa trạm \"{station.Name}\" vì đang được gán vào {routeCount} tuyến xe hoặc {bookingCount} vé đặt!";
                return RedirectToAction(nameof(Index));
            }

            _context.Stations.Remove(station);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã xóa trạm dừng \"{station.Name}\" thành công!";
            return RedirectToAction(nameof(Index));
        }

        private bool StationExists(int id)
        {
            return _context.Stations.Any(e => e.StationId == id);
        }
    }
}

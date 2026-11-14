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
    public class RoutesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RoutesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: RouteManager/Routes
        public async Task<IActionResult> Index(string? searchProvince, bool? isActive, int page = 1)
        {
            var query = _context.Routes
                .Include(r => r.RouteStations)
                    .ThenInclude(rs => rs.Station)
                .Include(r => r.Trips)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchProvince))
            {
                var sp = searchProvince.Trim();
                query = query.Where(r => r.StartProvince.Contains(sp) || r.EndProvince.Contains(sp));
            }

            if (isActive.HasValue)
            {
                query = query.Where(r => r.IsActive == isActive.Value);
            }

            ViewBag.SearchProvince = searchProvince;
            ViewBag.IsActiveFilter = isActive;

            var totalItems = await query.CountAsync();
            int pageSize = 15;
            page = Math.Max(1, page);

            var items = await query
                .OrderBy(r => r.StartProvince)
                .ThenBy(r => r.EndProvince)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var paginatedList = new PaginatedList<Route>(items, totalItems, page, pageSize);
            return View(paginatedList);
        }

        // GET: RouteManager/Routes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var route = await _context.Routes
                .Include(r => r.RouteStations.OrderBy(rs => rs.StopOrder))
                    .ThenInclude(rs => rs.Station)
                .Include(r => r.Trips.OrderByDescending(t => t.DepartureTime))
                    .ThenInclude(t => t.Bus)
                .FirstOrDefaultAsync(m => m.RouteId == id);

            if (route == null) return NotFound();

            return View(route);
        }

        // GET: RouteManager/Routes/Create
        public async Task<IActionResult> Create()
        {
            await PopulateStationsDropDownList();
            var model = new RouteFormViewModel
            {
                DistanceKm = 100,
                DurationHours = 2.5m,
                IsActive = true
            };
            return View(model);
        }

        // POST: RouteManager/Routes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RouteFormViewModel model)
        {
            if (ModelState.IsValid)
            {
                var route = new Route
                {
                    StartProvince = model.StartProvince.Trim(),
                    EndProvince = model.EndProvince.Trim(),
                    DistanceKm = model.DistanceKm,
                    DurationHours = model.DurationHours,
                    IsActive = model.IsActive
                };

                // Add configured RouteStations
                if (model.RouteStations != null && model.RouteStations.Any())
                {
                    int autoOrder = 1;
                    foreach (var s in model.RouteStations.OrderBy(x => x.StopOrder))
                    {
                        if (s.StationId > 0)
                        {
                            route.RouteStations.Add(new RouteStation
                            {
                                StationId = s.StationId,
                                StopOrder = s.StopOrder > 0 ? s.StopOrder : autoOrder,
                                StationType = s.StationType,
                                OffsetMinutes = s.OffsetMinutes
                            });
                            autoOrder++;
                        }
                    }
                }

                _context.Routes.Add(route);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Đã tạo tuyến xe \"{route.StartProvince} - {route.EndProvince}\" với {route.RouteStations.Count} trạm dừng thành công!";
                return RedirectToAction(nameof(Index));
            }

            await PopulateStationsDropDownList();
            return View(model);
        }

        // GET: RouteManager/Routes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var route = await _context.Routes
                .Include(r => r.RouteStations.OrderBy(rs => rs.StopOrder))
                    .ThenInclude(rs => rs.Station)
                .FirstOrDefaultAsync(m => m.RouteId == id);

            if (route == null) return NotFound();

            var model = new RouteFormViewModel
            {
                RouteId = route.RouteId,
                StartProvince = route.StartProvince,
                EndProvince = route.EndProvince,
                DistanceKm = route.DistanceKm,
                DurationHours = route.DurationHours,
                IsActive = route.IsActive,
                RouteStations = route.RouteStations.Select(rs => new RouteStationItemViewModel
                {
                    Id = rs.Id,
                    StationId = rs.StationId,
                    StationName = rs.Station != null ? $"{rs.Station.Name} ({rs.Station.Province})" : "",
                    StopOrder = rs.StopOrder,
                    StationType = rs.StationType,
                    OffsetMinutes = rs.OffsetMinutes
                }).ToList()
            };

            await PopulateStationsDropDownList();
            return View(model);
        }

        // POST: RouteManager/Routes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, RouteFormViewModel model)
        {
            if (id != model.RouteId) return NotFound();

            if (ModelState.IsValid)
            {
                var route = await _context.Routes
                    .Include(r => r.RouteStations)
                    .FirstOrDefaultAsync(r => r.RouteId == id);

                if (route == null) return NotFound();

                route.StartProvince = model.StartProvince.Trim();
                route.EndProvince = model.EndProvince.Trim();
                route.DistanceKm = model.DistanceKm;
                route.DurationHours = model.DurationHours;
                route.IsActive = model.IsActive;

                // Sync RouteStations
                // Remove existing ones
                _context.RouteStations.RemoveRange(route.RouteStations);

                // Add updated ones
                if (model.RouteStations != null && model.RouteStations.Any())
                {
                    int autoOrder = 1;
                    foreach (var s in model.RouteStations.OrderBy(x => x.StopOrder))
                    {
                        if (s.StationId > 0)
                        {
                            route.RouteStations.Add(new RouteStation
                            {
                                RouteId = route.RouteId,
                                StationId = s.StationId,
                                StopOrder = s.StopOrder > 0 ? s.StopOrder : autoOrder,
                                StationType = s.StationType,
                                OffsetMinutes = s.OffsetMinutes
                            });
                            autoOrder++;
                        }
                    }
                }

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã cập nhật tuyến \"{route.StartProvince} - {route.EndProvince}\" và danh sách trạm dừng thành công!";
                return RedirectToAction(nameof(Index));
            }

            await PopulateStationsDropDownList();
            return View(model);
        }

        // GET: RouteManager/Routes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var route = await _context.Routes
                .Include(r => r.RouteStations)
                    .ThenInclude(rs => rs.Station)
                .Include(r => r.Trips)
                .FirstOrDefaultAsync(m => m.RouteId == id);

            if (route == null) return NotFound();

            ViewBag.TripsCount = route.Trips.Count;
            return View(route);
        }

        // POST: RouteManager/Routes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var route = await _context.Routes
                .Include(r => r.Trips)
                .Include(r => r.RouteStations)
                .FirstOrDefaultAsync(r => r.RouteId == id);

            if (route == null) return NotFound();

            if (route.Trips.Any())
            {
                TempData["ErrorMessage"] = $"Không thể xóa tuyến \"{route.StartProvince} - {route.EndProvince}\" vì đã có {route.Trips.Count} chuyến xe được tạo!";
                return RedirectToAction(nameof(Index));
            }

            _context.Routes.Remove(route);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã xóa tuyến đường \"{route.StartProvince} - {route.EndProvince}\" thành công!";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateStationsDropDownList()
        {
            var stations = await _context.Stations
                .OrderBy(s => s.Province)
                .ThenBy(s => s.Name)
                .Select(s => new
                {
                    s.StationId,
                    DisplayText = $"{s.Name} ({s.Province})"
                })
                .ToListAsync();

            ViewBag.StationList = new SelectList(stations, "StationId", "DisplayText");
        }
    }
}

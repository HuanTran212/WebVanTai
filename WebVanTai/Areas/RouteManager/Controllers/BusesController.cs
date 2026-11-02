using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebVanTai.Data;
using WebVanTai.Models;

namespace WebVanTai.Areas.RouteManager.Controllers
{
    [Area("RouteManager")]
    [Authorize(Roles = "RouteManager,Admin")]
    public class BusesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BusesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: RouteManager/Buses
        public async Task<IActionResult> Index(string? searchLicensePlate, int? busTypeId, bool? isActive)
        {
            var query = _context.Buses
                .Include(b => b.BusType)
                .Include(b => b.Trips)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchLicensePlate))
            {
                var search = searchLicensePlate.Trim();
                query = query.Where(b => b.LicensePlate.Contains(search));
            }

            if (busTypeId.HasValue)
            {
                query = query.Where(b => b.BusTypeId == busTypeId.Value);
            }

            if (isActive.HasValue)
            {
                query = query.Where(b => b.IsActive == isActive.Value);
            }

            ViewBag.BusTypes = new SelectList(await _context.BusTypes.OrderBy(bt => bt.Name).ToListAsync(), "BusTypeId", "Name", busTypeId);
            ViewBag.SearchLicensePlate = searchLicensePlate;
            ViewBag.SelectedBusTypeId = busTypeId;
            ViewBag.IsActiveFilter = isActive;

            var buses = await query.OrderBy(b => b.LicensePlate).ToListAsync();
            return View(buses);
        }

        // GET: RouteManager/Buses/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var bus = await _context.Buses
                .Include(b => b.BusType)
                .Include(b => b.Trips.OrderByDescending(t => t.DepartureTime))
                    .ThenInclude(t => t.Route)
                .FirstOrDefaultAsync(m => m.BusId == id);

            if (bus == null) return NotFound();

            return View(bus);
        }

        // GET: RouteManager/Buses/Create
        public async Task<IActionResult> Create()
        {
            await PopulateBusTypesDropDownList();
            var model = new Bus
            {
                Amenities = "Wifi tốc độ cao, Nước uống, Khăn lạnh, Cổng sạc USB",
                IsActive = true
            };
            return View(model);
        }

        // POST: RouteManager/Buses/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("BusId,BusTypeId,LicensePlate,Amenities,IsActive")] Bus bus)
        {
            bus.LicensePlate = bus.LicensePlate?.Trim().ToUpper() ?? string.Empty;

            // Check unique license plate
            if (await _context.Buses.AnyAsync(b => b.LicensePlate == bus.LicensePlate))
            {
                ModelState.AddModelError("LicensePlate", $"Biển số xe \"{bus.LicensePlate}\" đã tồn tại trong hệ thống!");
            }

            if (ModelState.IsValid)
            {
                _context.Add(bus);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã thêm xe biển số \"{bus.LicensePlate}\" thành công!";
                return RedirectToAction(nameof(Index));
            }

            await PopulateBusTypesDropDownList(bus.BusTypeId);
            return View(bus);
        }

        // GET: RouteManager/Buses/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var bus = await _context.Buses.FindAsync(id);
            if (bus == null) return NotFound();

            await PopulateBusTypesDropDownList(bus.BusTypeId);
            return View(bus);
        }

        // POST: RouteManager/Buses/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("BusId,BusTypeId,LicensePlate,Amenities,IsActive")] Bus bus)
        {
            if (id != bus.BusId) return NotFound();

            bus.LicensePlate = bus.LicensePlate?.Trim().ToUpper() ?? string.Empty;

            // Check unique license plate excluding current
            if (await _context.Buses.AnyAsync(b => b.LicensePlate == bus.LicensePlate && b.BusId != id))
            {
                ModelState.AddModelError("LicensePlate", $"Biển số xe \"{bus.LicensePlate}\" đã được đăng ký bởi xe khác!");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(bus);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Đã cập nhật thông tin xe biển số \"{bus.LicensePlate}\" thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BusExists(bus.BusId))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }

            await PopulateBusTypesDropDownList(bus.BusTypeId);
            return View(bus);
        }

        // GET: RouteManager/Buses/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var bus = await _context.Buses
                .Include(b => b.BusType)
                .Include(b => b.Trips)
                .FirstOrDefaultAsync(m => m.BusId == id);

            if (bus == null) return NotFound();

            ViewBag.TripsCount = bus.Trips.Count;
            return View(bus);
        }

        // POST: RouteManager/Buses/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var bus = await _context.Buses
                .Include(b => b.Trips)
                .FirstOrDefaultAsync(m => m.BusId == id);

            if (bus == null) return NotFound();

            if (bus.Trips.Any())
            {
                TempData["ErrorMessage"] = $"Không thể xóa xe \"{bus.LicensePlate}\" vì đã có {bus.Trips.Count} chuyến xe được xếp lịch!";
                return RedirectToAction(nameof(Index));
            }

            _context.Buses.Remove(bus);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã xóa xe biển số \"{bus.LicensePlate}\" thành công!";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateBusTypesDropDownList(int? selectedId = null)
        {
            var busTypes = await _context.BusTypes
                .OrderBy(bt => bt.Name)
                .Select(bt => new
                {
                    bt.BusTypeId,
                    DisplayText = $"{bt.Name} ({bt.TotalSeats} chỗ, {bt.TotalDecks} tầng)"
                })
                .ToListAsync();

            ViewBag.BusTypeId = new SelectList(busTypes, "BusTypeId", "DisplayText", selectedId);
        }

        private bool BusExists(int id)
        {
            return _context.Buses.Any(e => e.BusId == id);
        }
    }
}

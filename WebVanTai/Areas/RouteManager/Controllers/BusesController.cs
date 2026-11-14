using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebVanTai.Data;
using WebVanTai.Models;
using WebVanTai.Models.Enums;

using WebVanTai.Models.ViewModels;

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
        public async Task<IActionResult> Index(string? searchLicensePlate, int? busTypeId, bool? isActive, int page = 1)
        {
            var query = _context.Buses
                .Include(b => b.BusType)
                .Include(b => b.Driver)
                .Include(b => b.Assistant)
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

            var totalItems = await query.CountAsync();
            int pageSize = 15;
            page = Math.Max(1, page);

            var items = await query
                .OrderBy(b => b.LicensePlate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var paginatedList = new PaginatedList<Bus>(items, totalItems, page, pageSize);
            return View(paginatedList);
        }

        // GET: RouteManager/Buses/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var bus = await _context.Buses
                .Include(b => b.BusType)
                .Include(b => b.Driver)
                .Include(b => b.Assistant)
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
            await PopulateCrewDropDownListsAsync();
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
        public async Task<IActionResult> Create([Bind("BusId,BusTypeId,LicensePlate,Amenities,IsActive,DriverId,AssistantId")] Bus bus)
        {
            bus.LicensePlate = bus.LicensePlate?.Trim().ToUpper() ?? string.Empty;

            // Check unique license plate
            if (await _context.Buses.AnyAsync(b => b.LicensePlate == bus.LicensePlate))
            {
                ModelState.AddModelError("LicensePlate", $"Biển số xe \"{bus.LicensePlate}\" đã tồn tại trong hệ thống!");
            }

            await ValidateCrewAssignmentAsync(bus, currentBusId: 0);

            if (ModelState.IsValid)
            {
                _context.Add(bus);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã thêm xe biển số \"{bus.LicensePlate}\" thành công!";
                return RedirectToAction(nameof(Index));
            }

            await PopulateBusTypesDropDownList(bus.BusTypeId);
            await PopulateCrewDropDownListsAsync(bus.DriverId, bus.AssistantId);
            return View(bus);
        }

        // GET: RouteManager/Buses/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var bus = await _context.Buses.FindAsync(id);
            if (bus == null) return NotFound();

            await PopulateBusTypesDropDownList(bus.BusTypeId);
            await PopulateCrewDropDownListsAsync(bus.DriverId, bus.AssistantId, currentBusId: bus.BusId);
            return View(bus);
        }

        // POST: RouteManager/Buses/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("BusId,BusTypeId,LicensePlate,Amenities,IsActive,DriverId,AssistantId")] Bus bus)
        {
            if (id != bus.BusId) return NotFound();

            bus.LicensePlate = bus.LicensePlate?.Trim().ToUpper() ?? string.Empty;

            // Check unique license plate excluding current
            if (await _context.Buses.AnyAsync(b => b.LicensePlate == bus.LicensePlate && b.BusId != id))
            {
                ModelState.AddModelError("LicensePlate", $"Biển số xe \"{bus.LicensePlate}\" đã được đăng ký bởi xe khác!");
            }

            await ValidateCrewAssignmentAsync(bus, currentBusId: id);

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
            await PopulateCrewDropDownListsAsync(bus.DriverId, bus.AssistantId, currentBusId: id);
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

        /// <summary>
        /// Dropdown phân công Tài xế / Lơ xe; hiển thị cả nhân sự đang phục vụ xe khác
        /// để điều hành dễ nhận biết (gán trùng sẽ bị chặn ở ValidateCrewAssignmentAsync).
        /// </summary>
        private async Task PopulateCrewDropDownListsAsync(int? selectedDriverId = null, int? selectedAssistantId = null, int currentBusId = 0)
        {
            var assignedBuses = await _context.Buses
                .AsNoTracking()
                .Where(b => b.BusId != currentBusId)
                .Select(b => new { b.LicensePlate, b.DriverId, b.AssistantId })
                .ToListAsync();

            var driverMap = assignedBuses.Where(b => b.DriverId.HasValue).ToDictionary(b => b.DriverId!.Value, b => b.LicensePlate);
            var assistantMap = assignedBuses.Where(b => b.AssistantId.HasValue).ToDictionary(b => b.AssistantId!.Value, b => b.LicensePlate);

            var drivers = await _context.CrewMembers
                .AsNoTracking()
                .Where(c => c.Role == CrewRole.Driver && (c.IsActive || c.CrewMemberId == selectedDriverId))
                .OrderBy(c => c.FullName)
                .ToListAsync();

            var assistants = await _context.CrewMembers
                .AsNoTracking()
                .Where(c => c.Role == CrewRole.Assistant && (c.IsActive || c.CrewMemberId == selectedAssistantId))
                .OrderBy(c => c.FullName)
                .ToListAsync();

            ViewBag.DriverId = new SelectList(drivers.Select(d => new
            {
                d.CrewMemberId,
                DisplayText = driverMap.TryGetValue(d.CrewMemberId, out var plate)
                    ? $"{d.FullName} ({d.PhoneNumber}) [Đang phụ trách {plate}]"
                    : $"{d.FullName} ({d.PhoneNumber})"
            }), "CrewMemberId", "DisplayText", selectedDriverId);

            ViewBag.AssistantId = new SelectList(assistants.Select(a => new
            {
                a.CrewMemberId,
                DisplayText = assistantMap.TryGetValue(a.CrewMemberId, out var plate)
                    ? $"{a.FullName} ({a.PhoneNumber}) [Đang phụ trách {plate}]"
                    : $"{a.FullName} ({a.PhoneNumber})"
            }), "CrewMemberId", "DisplayText", selectedAssistantId);
        }

        /// <summary>Chặn gán 1 tài xế / lơ xe cho 2 xe khách cùng lúc.</summary>
        private async Task ValidateCrewAssignmentAsync(Bus bus, int currentBusId)
        {
            if (bus.DriverId.HasValue)
            {
                var otherBus = await _context.Buses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.BusId != currentBusId && b.DriverId == bus.DriverId.Value);
                if (otherBus != null)
                {
                    ModelState.AddModelError(nameof(bus.DriverId), $"Tài xế này đang được phân công cho xe {otherBus.LicensePlate}. Vui lòng chọn tài xế khác hoặc gỡ phân công ở xe kia trước.");
                }
            }

            if (bus.AssistantId.HasValue)
            {
                var otherBus = await _context.Buses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(b => b.BusId != currentBusId && b.AssistantId == bus.AssistantId.Value);
                if (otherBus != null)
                {
                    ModelState.AddModelError(nameof(bus.AssistantId), $"Lơ xe này đang được phân công cho xe {otherBus.LicensePlate}. Vui lòng chọn lơ xe khác hoặc gỡ phân công ở xe kia trước.");
                }
            }
        }

        private bool BusExists(int id)
        {
            return _context.Buses.Any(e => e.BusId == id);
        }
    }
}

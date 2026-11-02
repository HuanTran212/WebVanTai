using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebVanTai.Data;
using WebVanTai.Models;

namespace WebVanTai.Areas.RouteManager.Controllers
{
    [Area("RouteManager")]
    [Authorize(Roles = "RouteManager,Admin")]
    public class BusTypesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BusTypesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: RouteManager/BusTypes
        public async Task<IActionResult> Index()
        {
            var busTypes = await _context.BusTypes
                .Include(bt => bt.Buses)
                .OrderBy(bt => bt.Name)
                .ToListAsync();

            return View(busTypes);
        }

        // GET: RouteManager/BusTypes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var busType = await _context.BusTypes
                .Include(bt => bt.Buses)
                .FirstOrDefaultAsync(m => m.BusTypeId == id);

            if (busType == null) return NotFound();

            return View(busType);
        }

        // GET: RouteManager/BusTypes/Create
        public IActionResult Create()
        {
            var model = new BusType
            {
                TotalSeats = 34,
                TotalDecks = 2,
                SeatLayoutJson = GenerateDefaultSeatLayout(34, 2)
            };
            return View(model);
        }

        // POST: RouteManager/BusTypes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("BusTypeId,Name,TotalSeats,TotalDecks,SeatLayoutJson")] BusType busType)
        {
            // Validate SeatLayoutJson is valid JSON
            if (!string.IsNullOrWhiteSpace(busType.SeatLayoutJson))
            {
                try
                {
                    JsonDocument.Parse(busType.SeatLayoutJson);
                }
                catch (JsonException ex)
                {
                    ModelState.AddModelError("SeatLayoutJson", $"Cấu trúc JSON sơ đồ ghế không hợp lệ: {ex.Message}");
                }
            }

            if (ModelState.IsValid)
            {
                _context.Add(busType);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã thêm loại xe \"{busType.Name}\" thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(busType);
        }

        // GET: RouteManager/BusTypes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var busType = await _context.BusTypes.FindAsync(id);
            if (busType == null) return NotFound();

            return View(busType);
        }

        // POST: RouteManager/BusTypes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("BusTypeId,Name,TotalSeats,TotalDecks,SeatLayoutJson")] BusType busType)
        {
            if (id != busType.BusTypeId) return NotFound();

            if (!string.IsNullOrWhiteSpace(busType.SeatLayoutJson))
            {
                try
                {
                    JsonDocument.Parse(busType.SeatLayoutJson);
                }
                catch (JsonException ex)
                {
                    ModelState.AddModelError("SeatLayoutJson", $"Cấu trúc JSON sơ đồ ghế không hợp lệ: {ex.Message}");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(busType);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Đã cập nhật loại xe \"{busType.Name}\" thành công!";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BusTypeExists(busType.BusTypeId))
                        return NotFound();
                    else
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(busType);
        }

        // GET: RouteManager/BusTypes/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var busType = await _context.BusTypes
                .Include(bt => bt.Buses)
                .FirstOrDefaultAsync(m => m.BusTypeId == id);

            if (busType == null) return NotFound();

            ViewBag.BusesCount = busType.Buses.Count;
            return View(busType);
        }

        // POST: RouteManager/BusTypes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var busType = await _context.BusTypes
                .Include(bt => bt.Buses)
                .FirstOrDefaultAsync(m => m.BusTypeId == id);

            if (busType == null) return NotFound();

            if (busType.Buses.Any())
            {
                TempData["ErrorMessage"] = $"Không thể xóa loại xe \"{busType.Name}\" vì đang có {busType.Buses.Count} xe thuộc loại này!";
                return RedirectToAction(nameof(Index));
            }

            _context.BusTypes.Remove(busType);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã xóa loại xe \"{busType.Name}\" thành công!";
            return RedirectToAction(nameof(Index));
        }

        // Quick API to generate standard JSON layout
        [HttpGet]
        public IActionResult GenerateLayout(int totalSeats, int totalDecks, int cols = 3)
        {
            var json = GenerateDefaultSeatLayout(totalSeats, totalDecks, cols);
            return Content(json, "application/json");
        }

        private static string GenerateDefaultSeatLayout(int totalSeats, int totalDecks, int cols = 3)
        {
            if (totalDecks <= 0) totalDecks = 1;
            if (totalSeats <= 0) totalSeats = 20;
            if (cols <= 0) cols = 3;

            int seatsPerDeck = (int)Math.Ceiling((double)totalSeats / totalDecks);
            var decks = new List<object>();

            for (int d = 1; d <= totalDecks; d++)
            {
                var prefix = d == 1 ? "A" : (d == 2 ? "B" : $"D{d}");
                var deckName = d == 1 ? "Tầng dưới (A)" : (d == 2 ? "Tầng trên (B)" : $"Tầng {d}");
                var seats = new List<object>();

                int currentDeckSeats = (d == totalDecks) ? (totalSeats - (seatsPerDeck * (totalDecks - 1))) : seatsPerDeck;
                if (currentDeckSeats <= 0) currentDeckSeats = seatsPerDeck;

                for (int s = 1; s <= currentDeckSeats; s++)
                {
                    int row = ((s - 1) / cols) + 1;
                    int col = ((s - 1) % cols) + 1;
                    string code = $"{prefix}{s:D2}";

                    seats.Add(new
                    {
                        code = code,
                        row = row,
                        col = col,
                        type = "Standard"
                    });
                }

                decks.Add(new
                {
                    deckNumber = d,
                    deckName = deckName,
                    rows = (int)Math.Ceiling((double)currentDeckSeats / cols),
                    cols = cols,
                    seats = seats
                });
            }

            return JsonSerializer.Serialize(new { decks }, new JsonSerializerOptions { WriteIndented = true });
        }

        private bool BusTypeExists(int id)
        {
            return _context.BusTypes.Any(e => e.BusTypeId == id);
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebVanTai.Data;
using WebVanTai.Models;
using WebVanTai.Models.Enums;

namespace WebVanTai.Areas.RouteManager.Controllers
{
    /// <summary>
    /// Quản lý danh sách Tài xế &amp; Lơ xe (tổ lái) để phân công cố định theo từng xe khách.
    /// </summary>
    [Area("RouteManager")]
    [Authorize(Roles = "RouteManager,Admin")]
    public class CrewMembersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CrewMembersController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(CrewRole? role, string? search)
        {
            var query = _context.CrewMembers
                .Include(c => c.DrivenBuses)
                .Include(c => c.AssistedBuses)
                .AsQueryable();

            if (role.HasValue)
                query = query.Where(c => c.Role == role.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(c =>
                    c.FullName.Contains(term) ||
                    c.PhoneNumber.Contains(term) ||
                    (c.LicenseNumber != null && c.LicenseNumber.Contains(term)));
            }

            ViewBag.SelectedRole = role;
            ViewBag.Search = search;
            var items = await query.OrderBy(c => c.Role).ThenBy(c => c.FullName).ToListAsync();
            return View(items);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CrewMember { IsActive = true, Role = CrewRole.Driver });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FullName,PhoneNumber,LicenseNumber,Role,IsActive")] CrewMember member)
        {
            member.FullName = member.FullName?.Trim() ?? string.Empty;
            member.PhoneNumber = member.PhoneNumber?.Trim() ?? string.Empty;
            member.LicenseNumber = string.IsNullOrWhiteSpace(member.LicenseNumber) ? null : member.LicenseNumber.Trim().ToUpperInvariant();
            member.CreatedAt = DateTime.UtcNow;

            if (member.Role == CrewRole.Driver && string.IsNullOrWhiteSpace(member.LicenseNumber))
            {
                ModelState.AddModelError(nameof(member.LicenseNumber), "Vui lòng nhập số GPLX đối với Tài xế.");
            }

            if (ModelState.IsValid)
            {
                _context.CrewMembers.Add(member);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã thêm {(member.Role == CrewRole.Driver ? "Tài xế" : "Lơ xe")} {member.FullName}.";
                return RedirectToAction(nameof(Index));
            }

            return View(member);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var member = await _context.CrewMembers.FindAsync(id);
            if (member == null) return NotFound();
            return View(member);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("CrewMemberId,FullName,PhoneNumber,LicenseNumber,Role,IsActive")] CrewMember input)
        {
            if (id != input.CrewMemberId) return NotFound();

            var member = await _context.CrewMembers
                .Include(c => c.DrivenBuses)
                .Include(c => c.AssistedBuses)
                .FirstOrDefaultAsync(c => c.CrewMemberId == id);
            if (member == null) return NotFound();

            if (member.Role != input.Role && (member.DrivenBuses.Any() || member.AssistedBuses.Any()))
            {
                ModelState.AddModelError(nameof(input.Role), "Nhân sự đang được phân công theo xe, vui lòng gỡ phân công trước khi đổi vai trò.");
            }

            if (input.Role == CrewRole.Driver && string.IsNullOrWhiteSpace(input.LicenseNumber))
            {
                ModelState.AddModelError(nameof(input.LicenseNumber), "Vui lòng nhập số GPLX đối với Tài xế.");
            }

            if (ModelState.IsValid)
            {
                member.FullName = input.FullName.Trim();
                member.PhoneNumber = input.PhoneNumber.Trim();
                member.LicenseNumber = string.IsNullOrWhiteSpace(input.LicenseNumber) ? null : input.LicenseNumber.Trim().ToUpperInvariant();
                member.Role = input.Role;
                member.IsActive = input.IsActive;

                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã cập nhật thông tin {member.FullName}.";
                return RedirectToAction(nameof(Index));
            }

            return View(input);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var member = await _context.CrewMembers
                .Include(c => c.DrivenBuses)
                .Include(c => c.AssistedBuses)
                .FirstOrDefaultAsync(c => c.CrewMemberId == id);

            if (member == null) return NotFound();

            if (member.DrivenBuses.Any() || member.AssistedBuses.Any())
            {
                TempData["ErrorMessage"] = $"Không thể xóa {member.FullName} vì đang được phân công cho xe khách.";
                return RedirectToAction(nameof(Index));
            }

            _context.CrewMembers.Remove(member);
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã xóa nhân sự {member.FullName}.";
            return RedirectToAction(nameof(Index));
        }
    }
}

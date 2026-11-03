using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebVanTai.Data;
using WebVanTai.Models;
using WebVanTai.Areas.Admin.ViewModels;

namespace WebVanTai.Areas.Admin.Controllers
{
    /// <summary>
    /// Quản lý tài khoản nhân sự: cấp quyền (Admin / RouteManager / TicketInspector),
    /// khóa & mở khóa tài khoản, đặt lại mật khẩu.
    /// </summary>
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        /// <summary>Các vai trò nhân sự được phép cấp từ phân hệ Admin (không bao gồm Customer - khách tự đăng ký).</summary>
        private static readonly string[] StaffRoles = { "Admin", "RouteManager", "TicketInspector" };

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UsersController> _logger;

        public UsersController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context,
            ILogger<UsersController> logger)
        {
            _userManager = userManager;
            _context = context;
            _logger = logger;
        }

        // ==================== DANH SÁCH ====================

        [HttpGet]
        public async Task<IActionResult> Index(string? search, string? role)
        {
            var users = await _userManager.Users
                .AsNoTracking()
                .OrderByDescending(u => u.CreatedAt)
                .ToListAsync();

            // Tra vai trò của tất cả user trong một truy vấn
            var roleNames = await _context.Roles.ToDictionaryAsync(r => r.Id, r => r.Name);
            var userRoles = await _context.UserRoles.ToListAsync();
            var rolesByUser = userRoles
                .GroupBy(ur => ur.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(ur => roleNames.TryGetValue(ur.RoleId, out var name) ? name ?? "" : "")
                          .Where(n => !string.IsNullOrEmpty(n))
                          .ToList());

            var bookingCounts = await _context.Bookings
                .GroupBy(b => b.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count);

            var currentUserId = _userManager.GetUserId(User);

            var items = users.Select(u =>
            {
                var roles = rolesByUser.TryGetValue(u.Id, out var r) ? r : new List<string>();
                return new StaffUserListItem
                {
                    Id = u.Id,
                    Email = u.Email ?? string.Empty,
                    FullName = u.FullName,
                    PhoneNumber = u.PhoneNumber,
                    Roles = roles,
                    CreatedAt = u.CreatedAt,
                    IsLockedOut = u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow,
                    LockoutEnd = u.LockoutEnd,
                    BookingCount = bookingCounts.TryGetValue(u.Id, out var c) ? c : 0,
                    IsCurrentUser = u.Id == currentUserId
                };
            });

            // Lọc: mặc định chỉ hiện nhân sự (loại Customer), hoặc lọc theo vai trò được chọn
            if (!string.IsNullOrWhiteSpace(role) && role != "Staff" && role != "All")
            {
                items = items.Where(u => u.Roles.Contains(role));
            }
            else if (string.IsNullOrWhiteSpace(role) || role == "Staff")
            {
                items = items.Where(u => u.Roles.Any(r => StaffRoles.Contains(r)));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                items = items.Where(u =>
                    u.FullName.ToLower().Contains(term) ||
                    u.Email.ToLower().Contains(term) ||
                    (u.PhoneNumber ?? "").Contains(term));
            }

            ViewBag.Search = search;
            ViewBag.SelectedRole = role ?? "Staff";
            ViewBag.StaffRoles = StaffRoles;

            return View(items.ToList());
        }

        // ==================== TẠO MỚI ====================

        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.StaffRoles = StaffRoles;
            return View(new StaffCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StaffCreateViewModel model)
        {
            ViewBag.StaffRoles = StaffRoles;

            if (!StaffRoles.Contains(model.Role))
            {
                ModelState.AddModelError(nameof(model.Role), "Vai trò không hợp lệ cho tài khoản nhân sự.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var existing = await _userManager.FindByEmailAsync(model.Email);
            if (existing != null)
            {
                ModelState.AddModelError(nameof(model.Email), "Email này đã được sử dụng cho một tài khoản khác.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                EmailConfirmed = true,
                FullName = model.FullName.Trim(),
                PhoneNumber = model.PhoneNumber?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(model);
            }

            await _userManager.AddToRoleAsync(user, model.Role);

            _logger.LogInformation("Admin {Admin} đã tạo tài khoản nhân sự {Email} với vai trò {Role}",
                User.Identity?.Name, model.Email, model.Role);

            TempData["SuccessMessage"] = $"Đã tạo tài khoản {model.Email} với vai trò {model.Role}.";
            return RedirectToAction(nameof(Index));
        }

        // ==================== CHỈNH SỬA & CẤP QUYỀN ====================

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy tài khoản.";
                return RedirectToAction(nameof(Index));
            }

            var roles = await _userManager.GetRolesAsync(user);

            var model = new StaffEditViewModel
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                Role = roles.FirstOrDefault(r => StaffRoles.Contains(r)) ?? "TicketInspector",
                CurrentRoles = roles.ToList(),
                IsLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow
            };

            ViewBag.StaffRoles = StaffRoles;
            ViewBag.IsCurrentUser = user.Id == _userManager.GetUserId(User);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(StaffEditViewModel model)
        {
            ViewBag.StaffRoles = StaffRoles;
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy tài khoản.";
                return RedirectToAction(nameof(Index));
            }

            var isCurrentUser = user.Id == _userManager.GetUserId(User);
            ViewBag.IsCurrentUser = isCurrentUser;

            if (!StaffRoles.Contains(model.Role))
            {
                ModelState.AddModelError(nameof(model.Role), "Vai trò không hợp lệ cho tài khoản nhân sự.");
            }

            // Chặn tự hạ quyền Admin của chính mình (tránh mất quyền quản trị hệ thống)
            if (isCurrentUser && model.Role != "Admin")
            {
                ModelState.AddModelError(nameof(model.Role), "Bạn không thể thay đổi vai trò Admin của chính mình.");
            }

            if (!ModelState.IsValid)
            {
                model.Email = user.Email ?? string.Empty;
                model.CurrentRoles = (await _userManager.GetRolesAsync(user)).ToList();
                model.IsLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
                return View(model);
            }

            user.FullName = model.FullName.Trim();
            user.PhoneNumber = model.PhoneNumber?.Trim();
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                model.Email = user.Email ?? string.Empty;
                model.CurrentRoles = (await _userManager.GetRolesAsync(user)).ToList();
                return View(model);
            }

            // Thay thế vai trò: gỡ các vai trò nhân sự cũ, gán vai trò mới
            var currentRoles = await _userManager.GetRolesAsync(user);
            var rolesToRemove = currentRoles.Where(r => StaffRoles.Contains(r) && r != model.Role).ToList();
            if (rolesToRemove.Count > 0)
            {
                await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            }
            if (!currentRoles.Contains(model.Role))
            {
                await _userManager.AddToRoleAsync(user, model.Role);
            }

            _logger.LogInformation("Admin {Admin} đã cập nhật tài khoản {Email} sang vai trò {Role}",
                User.Identity?.Name, user.Email, model.Role);

            TempData["SuccessMessage"] = $"Đã cập nhật tài khoản {user.Email} (vai trò: {model.Role}).";
            return RedirectToAction(nameof(Index));
        }

        // ==================== KHÓA / MỞ KHÓA ====================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy tài khoản.";
                return RedirectToAction(nameof(Index));
            }

            if (user.Id == _userManager.GetUserId(User))
            {
                TempData["ErrorMessage"] = "Bạn không thể tự khóa tài khoản của chính mình.";
                return RedirectToAction(nameof(Index));
            }

            var isLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
            if (isLocked)
            {
                // Mở khóa: xóa hạn khóa + reset số lần đăng nhập sai
                user.LockoutEnd = null;
                await _userManager.UpdateAsync(user);
                await _userManager.ResetAccessFailedCountAsync(user);
                _logger.LogInformation("Admin {Admin} đã MỞ KHÓA tài khoản {Email}", User.Identity?.Name, user.Email);
                TempData["SuccessMessage"] = $"Đã mở khóa tài khoản {user.Email}. Nhân sự có thể đăng nhập bình thường.";
            }
            else
            {
                // Khóa 100 năm (bảo đảm khóa vĩnh viễn hiệu quả). LockoutEnabled phải bật thì khóa mới có hiệu lực.
                user.LockoutEnabled = true;
                user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
                await _userManager.UpdateAsync(user);
                _logger.LogInformation("Admin {Admin} đã KHÓA tài khoản {Email}", User.Identity?.Name, user.Email);
                TempData["SuccessMessage"] = $"Đã khóa tài khoản {user.Email}. Nhân sự không thể đăng nhập.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ==================== ĐẶT LẠI MẬT KHẨU ====================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string id, string newPassword)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy tài khoản.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            {
                TempData["ErrorMessage"] = "Mật khẩu mới phải có tối thiểu 6 ký tự.";
                return RedirectToAction(nameof(Edit), new { id });
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
            if (result.Succeeded)
            {
                _logger.LogInformation("Admin {Admin} đã đặt lại mật khẩu cho {Email}", User.Identity?.Name, user.Email);
                TempData["SuccessMessage"] = $"Đã đặt lại mật khẩu cho {user.Email}.";
            }
            else
            {
                TempData["ErrorMessage"] = string.Join(" ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(Edit), new { id });
        }
    }
}

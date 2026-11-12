using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebVanTai.Models;

namespace WebVanTai.Areas.RouteManager.Controllers
{
    [Area("RouteManager")]
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AccountController(
            SignInManager<ApplicationUser> signInManager, 
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [HttpGet("/Identity/Account/Login")]
        [HttpGet("/RouteManager/Account/Login")]
        [HttpGet("/Account/Login")]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (_signInManager.IsSignedIn(User))
            {
                return RedirectToAction("Index", "Home", new { area = "" });
            }
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost("/Identity/Account/Login")]
        [HttpPost("/RouteManager/Account/Login")]
        [HttpPost("/Account/Login")]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, bool rememberMe, string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.Email = email?.Trim();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError(string.Empty, "Vui lòng nhập đầy đủ Email/Tên đăng nhập và Mật khẩu.");
                return View();
            }

            var input = email.Trim();

            // 1. Tìm người dùng trong bảng AspNetUsers bằng cả Email hoặc UserName (hoặc Số điện thoại)
            var user = await _userManager.FindByEmailAsync(input) 
                       ?? await _userManager.FindByNameAsync(input);

            if (user == null)
            {
                user = await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == input);
            }

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản hoặc mật khẩu không chính xác.");
                return View();
            }

            // 2. Bắt buộc kiểm tra mật khẩu băm qua PasswordSignInAsync
            var result = await _signInManager.PasswordSignInAsync(
                user.UserName!, 
                password, 
                rememberMe, 
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                // Kiểm tra Role của user từ Database để điều hướng tự động
                var roles = await _userManager.GetRolesAsync(user);

                // Điều hướng thông minh theo quyền nếu không có returnUrl hợp lệ hoặc returnUrl mặc định/xung đột
                if (string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl) || returnUrl == "/" || returnUrl == "/RouteManager/Dashboard" || returnUrl.Contains("/Account/Login"))
                {
                    if (roles.Contains("Admin")) return LocalRedirect("/Admin/Dashboard");
                    if (roles.Contains("TicketManager")) return LocalRedirect("/TicketManager/Dashboard");
                    if (roles.Contains("TicketInspector")) return LocalRedirect("/TicketInspector/Dashboard");
                    if (roles.Contains("RouteManager")) return LocalRedirect("/RouteManager/Dashboard");
                    if (roles.Contains("Customer")) return LocalRedirect("/Payment/MyTickets");
                    return LocalRedirect("/");
                }

                // Chặn chuyển hướng sai quyền gây lỗi 403 Access Denied
                bool isForbidden =
                    (roles.Contains("TicketInspector") && !roles.Contains("Admin") && !roles.Contains("RouteManager") && (returnUrl.StartsWith("/RouteManager", StringComparison.OrdinalIgnoreCase) || returnUrl.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase) || returnUrl.StartsWith("/TicketManager", StringComparison.OrdinalIgnoreCase))) ||
                    (roles.Contains("TicketManager") && !roles.Contains("Admin") && (returnUrl.StartsWith("/RouteManager", StringComparison.OrdinalIgnoreCase) || returnUrl.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase) || returnUrl.StartsWith("/TicketInspector", StringComparison.OrdinalIgnoreCase))) ||
                    (roles.Contains("Customer") && !roles.Contains("Admin") && (returnUrl.StartsWith("/RouteManager", StringComparison.OrdinalIgnoreCase) || returnUrl.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase) || returnUrl.StartsWith("/TicketInspector", StringComparison.OrdinalIgnoreCase) || returnUrl.StartsWith("/TicketManager", StringComparison.OrdinalIgnoreCase))) ||
                    (roles.Contains("RouteManager") && !roles.Contains("Admin") && (returnUrl.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase) || returnUrl.StartsWith("/TicketManager", StringComparison.OrdinalIgnoreCase)));

                if (isForbidden)
                {
                    if (roles.Contains("Admin")) return LocalRedirect("/Admin/Dashboard");
                    if (roles.Contains("TicketManager")) return LocalRedirect("/TicketManager/Dashboard");
                    if (roles.Contains("TicketInspector")) return LocalRedirect("/TicketInspector/Dashboard");
                    if (roles.Contains("RouteManager")) return LocalRedirect("/RouteManager/Dashboard");
                    if (roles.Contains("Customer")) return LocalRedirect("/Payment/MyTickets");
                }
                return LocalRedirect(returnUrl);
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(string.Empty, "Tài khoản tạm thời bị khóa do nhập sai mật khẩu quá nhiều lần. Vui lòng thử lại sau.");
                return View();
            }

            ModelState.AddModelError(string.Empty, "Tài khoản hoặc mật khẩu không chính xác.");
            return View();
        }

        [HttpGet("/Identity/Account/Register")]
        [HttpGet("/RouteManager/Account/Register")]
        [HttpGet("/Account/Register")]
        [AllowAnonymous]
        public IActionResult Register(string? returnUrl = null)
        {
            if (_signInManager.IsSignedIn(User))
            {
                return RedirectToAction("Index", "Home", new { area = "" });
            }
            ViewBag.ReturnUrl = returnUrl ?? "/";
            return View();
        }

        [HttpPost("/Identity/Account/Register")]
        [HttpPost("/RouteManager/Account/Register")]
        [HttpPost("/Account/Register")]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            string fullName,
            string email,
            string? phoneNumber,
            string password,
            string confirmPassword,
            string? returnUrl = null)
        {
            returnUrl ??= "/";
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.FullName = fullName;
            ViewBag.Email = email;
            ViewBag.PhoneNumber = phoneNumber;

            if (string.IsNullOrWhiteSpace(fullName))
            {
                ModelState.AddModelError(string.Empty, "Vui lòng nhập Họ và tên.");
            }

            if (string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError(string.Empty, "Vui lòng nhập Địa chỉ Email.");
            }
            else if (!email.Contains("@") || !email.Contains("."))
            {
                ModelState.AddModelError(string.Empty, "Định dạng Email không hợp lệ.");
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError(string.Empty, "Vui lòng nhập Mật khẩu.");
            }
            else if (password.Length < 6)
            {
                ModelState.AddModelError(string.Empty, "Mật khẩu phải có độ dài tối thiểu 6 ký tự.");
            }
            else if (password != confirmPassword)
            {
                ModelState.AddModelError(string.Empty, "Mật khẩu xác nhận không khớp.");
            }

            if (!ModelState.IsValid)
            {
                return View();
            }

            var existingUser = await _userManager.FindByEmailAsync(email.Trim());
            if (existingUser != null)
            {
                ModelState.AddModelError(string.Empty, "Địa chỉ email này đã được sử dụng. Vui lòng đăng nhập hoặc dùng email khác.");
                return View();
            }

            var user = new ApplicationUser
            {
                UserName = email.Trim(),
                Email = email.Trim(),
                FullName = fullName.Trim(),
                PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim(),
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, password);
            if (result.Succeeded)
            {
                // Đảm bảo role Customer tồn tại
                if (!await _roleManager.RoleExistsAsync("Customer"))
                {
                    await _roleManager.CreateAsync(new IdentityRole("Customer"));
                }
                await _userManager.AddToRoleAsync(user, "Customer");

                // Đăng nhập ngay sau khi đăng ký
                await _signInManager.SignInAsync(user, isPersistent: false);
                TempData["SuccessMessage"] = $"Đăng ký thành công! Chào mừng {user.FullName} đến với G-Trans.";

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && returnUrl != "/RouteManager/Dashboard")
                {
                    return LocalRedirect(returnUrl);
                }
                return RedirectToAction("Index", "Home", new { area = "" });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View();
        }

        [HttpPost("/RouteManager/Account/Logout")]
        [HttpPost("/Identity/Account/Logout")]
        [HttpPost("/Admin/Account/Logout")]
        [HttpPost("/Account/Logout")]
        [HttpGet("/RouteManager/Account/Logout")]
        [HttpGet("/Identity/Account/Logout")]
        [HttpGet("/Admin/Account/Logout")]
        [HttpGet("/Account/Logout")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> Logout(string? returnUrl = null)
        {
            await _signInManager.SignOutAsync();
            TempData["SuccessMessage"] = "Đã đăng xuất thành công.";
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }
            return RedirectToAction("Index", "Home", new { area = "" });
        }

        [HttpGet("/Identity/Account/AccessDenied")]
        [HttpGet("/RouteManager/Account/AccessDenied")]
        [HttpGet("/Account/AccessDenied")]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl ?? "/RouteManager/Dashboard";
            return View();
        }

        [HttpPost("/Identity/Account/Login")]
        [HttpPost("/RouteManager/Account/Login")]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, bool rememberMe, string? returnUrl = null)
        {
            returnUrl ??= "/RouteManager/Dashboard";
            ViewBag.ReturnUrl = returnUrl;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError(string.Empty, "Vui lòng nhập đầy đủ Email và Mật khẩu.");
                return View();
            }

            var result = await _signInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: false);
            if (result.Succeeded)
            {
                var user = await _userManager.FindByEmailAsync(email);
                if (user != null)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    if (returnUrl == "/RouteManager/Dashboard")
                    {
                        if (roles.Contains("Admin")) return LocalRedirect("/Admin/Dashboard");
                        if (roles.Contains("TicketInspector")) return LocalRedirect("/TicketInspector/Dashboard");
                        if (roles.Contains("Customer")) return LocalRedirect("/Payment/MyTickets");
                    }
                }
                return LocalRedirect(returnUrl);
            }

            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác. Mặc định: routemanager@webvantai.vn / P@ssword123");
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

        [HttpGet("/Identity/Account/QuickLogin")]
        [HttpGet("/RouteManager/Account/QuickLogin")]
        [AllowAnonymous]
        public async Task<IActionResult> QuickLogin(string role = "RouteManager", string? returnUrl = null)
        {
            var email = role switch
            {
                "Admin" => "admin@webvantai.vn",
                "TicketInspector" => "inspector@webvantai.vn",
                "Customer" => "customer@webvantai.vn",
                _ => "routemanager@webvantai.vn"
            };
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null && role == "Customer")
            {
                user = await _userManager.FindByEmailAsync("customer@futabus.vn");
            }
            if (user != null)
            {
                await _signInManager.SignInAsync(user, isPersistent: true);
                TempData["SuccessMessage"] = $"Đăng nhập thành công với quyền [{role}] ({user.Email})!";
                if (!string.IsNullOrEmpty(returnUrl)) return LocalRedirect(returnUrl);
                return role switch
                {
                    "Admin" => LocalRedirect("/Admin/Dashboard"),
                    "TicketInspector" => LocalRedirect("/TicketInspector/Dashboard"),
                    "Customer" => LocalRedirect("/Payment/MyTickets"),
                    _ => LocalRedirect("/RouteManager/Dashboard")
                };
            }

            TempData["ErrorMessage"] = "Không tìm thấy tài khoản mẫu. Vui lòng đảm bảo database đã được seed.";
            return RedirectToAction("Login", "Account", new { area = "RouteManager", returnUrl });
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
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}

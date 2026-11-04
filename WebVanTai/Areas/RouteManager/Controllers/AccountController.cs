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

        public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager)
        {
            _signInManager = signInManager;
            _userManager = userManager;
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
                    if (!roles.Contains("RouteManager") && !roles.Contains("Admin"))
                    {
                        await _signInManager.SignOutAsync();
                        ModelState.AddModelError(string.Empty, "Tài khoản của bạn không có quyền truy cập vào phân hệ RouteManager (Cần vai trò RouteManager hoặc Admin).");
                        return View();
                    }
                }
                return LocalRedirect(returnUrl);
            }

            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác. Mặc định: routemanager@webvantai.vn / P@ssword123");
            return View();
        }

        [HttpGet("/Identity/Account/QuickLogin")]
        [HttpGet("/RouteManager/Account/QuickLogin")]
        [AllowAnonymous]
        public async Task<IActionResult> QuickLogin(string role = "RouteManager", string? returnUrl = null)
        {
            var email = role == "Admin" ? "admin@webvantai.vn" : "routemanager@webvantai.vn";
            var user = await _userManager.FindByEmailAsync(email);
            if (user != null)
            {
                await _signInManager.SignInAsync(user, isPersistent: true);
                TempData["SuccessMessage"] = $"Đăng nhập thành công với quyền [{role}] ({email})!";
                return LocalRedirect(returnUrl ?? "/RouteManager/Dashboard");
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

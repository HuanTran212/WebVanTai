using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebVanTai.Models;
using WebVanTai.Models.ViewModels;

namespace WebVanTai.Controllers
{
    /// <summary>
    /// Xem/đổi thông tin tài khoản và đổi mật khẩu cho mọi vai trò (khách hàng & nhân sự).
    /// </summary>
    [Authorize]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpGet]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Redirect("/Identity/Account/Login");

            return View(await BuildManagePageModelAsync(user));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile([Bind(Prefix = "Profile")] AccountProfileViewModel input)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Redirect("/Identity/Account/Login");

            if (!ModelState.IsValid)
            {
                var model = await BuildManagePageModelAsync(user);
                model.Profile.FullName = input.FullName;
                model.Profile.PhoneNumber = input.PhoneNumber;
                return View("Profile", model);
            }

            user.FullName = input.FullName.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim();

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var err in result.Errors)
                    ModelState.AddModelError(string.Empty, err.Description);

                var model = await BuildManagePageModelAsync(user);
                model.Profile.FullName = input.FullName;
                model.Profile.PhoneNumber = input.PhoneNumber;
                return View("Profile", model);
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["SuccessMessage"] = "Đã cập nhật thông tin tài khoản thành công.";
            return RedirectToAction(nameof(Profile));
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return RedirectToAction(nameof(Profile));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword([Bind(Prefix = "Password")] ChangePasswordViewModel input)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Redirect("/Identity/Account/Login");

            if (!ModelState.IsValid)
            {
                return View("Profile", await BuildManagePageModelAsync(user));
            }

            var result = await _userManager.ChangePasswordAsync(user, input.CurrentPassword, input.NewPassword);
            if (!result.Succeeded)
            {
                foreach (var err in result.Errors)
                    ModelState.AddModelError(string.Empty, err.Description);

                return View("Profile", await BuildManagePageModelAsync(user));
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
            return RedirectToAction(nameof(Profile));
        }

        private async Task<AccountManagePageViewModel> BuildManagePageModelAsync(ApplicationUser user)
        {
            var roles = (await _userManager.GetRolesAsync(user)).ToList();
            return new AccountManagePageViewModel
            {
                Profile = new AccountProfileViewModel
                {
                    Email = user.Email ?? string.Empty,
                    FullName = user.FullName,
                    PhoneNumber = user.PhoneNumber,
                    CreatedAt = user.CreatedAt,
                    Roles = roles
                },
                Password = new ChangePasswordViewModel()
            };
        }
    }
}

using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebVanTai.Data;
using WebVanTai.Models;
using WebVanTai.Models.Enums;
using WebVanTai.Services;

namespace WebVanTai.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly EmailService _emailService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            EmailService emailService,
            ILogger<HomeController> logger)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            // 1. Lấy danh sách các tỉnh đi và tỉnh đến từ các tuyến xe đang hoạt động
            var activeRoutes = await _context.Routes
                .Where(r => r.IsActive)
                .Include(r => r.Trips.Where(t => t.Status == TripStatus.Scheduled && t.DepartureTime >= DateTime.UtcNow.AddMinutes(-30)))
                    .ThenInclude(t => t.Bus)
                        .ThenInclude(b => b!.BusType)
                .ToListAsync();

            var startProvinces = activeRoutes
                .Select(r => r.StartProvince)
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            var endProvinces = activeRoutes
                .Select(r => r.EndProvince)
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            // Fallback nếu chưa có route nào: lấy từ Station
            if (!startProvinces.Any() || !endProvinces.Any())
            {
                var stationProvinces = await _context.Stations
                    .Select(s => s.Province)
                    .Distinct()
                    .OrderBy(p => p)
                    .ToListAsync();

                if (!startProvinces.Any()) startProvinces = stationProvinces;
                if (!endProvinces.Any()) endProvinces = stationProvinces;
            }

            // 2. Danh sách tuyến đường nổi bật (Popular Routes)
            var popularRoutes = activeRoutes.Select(r =>
            {
                var upcomingTrips = r.Trips.ToList();
                var minPrice = upcomingTrips.Any() ? upcomingTrips.Min(t => t.BasePrice) : 250000m;
                var busTypes = upcomingTrips
                    .Where(t => t.Bus?.BusType != null)
                    .Select(t => t.Bus!.BusType!.Name)
                    .Distinct()
                    .ToList();

                var busTypeSummary = busTypes.Any()
                    ? string.Join(", ", busTypes)
                    : "Giường nằm cao cấp, Limousine";

                // Chọn ảnh tượng trưng theo tỉnh đến
                var imageUrl = GetProvinceImageUrl(r.EndProvince);

                return new PopularRouteCardViewModel
                {
                    RouteId = r.RouteId,
                    StartProvince = r.StartProvince,
                    EndProvince = r.EndProvince,
                    DistanceKm = r.DistanceKm,
                    DurationHours = r.DurationHours,
                    MinPrice = minPrice,
                    TripCount = upcomingTrips.Count,
                    BusTypeSummary = busTypeSummary,
                    ImageUrl = imageUrl
                };
            }).ToList();

            var viewModel = new HomeViewModel
            {
                StartProvinces = startProvinces,
                EndProvinces = endProvinces,
                PopularRoutes = popularRoutes,
                TotalRoutes = activeRoutes.Count,
                TotalTrips = await _context.Trips.CountAsync(t => t.DepartureTime >= DateTime.UtcNow.Date),
                TotalStations = await _context.Stations.CountAsync()
            };

            return View(viewModel);
        }

        private static string GetProvinceImageUrl(string province)
        {
            var p = province.ToLowerInvariant();
            if (p.Contains("đà lạt") || p.Contains("lâm đồng"))
                return "https://images.unsplash.com/photo-1596401057633-54a8fe8ef647?auto=format&fit=crop&w=600&q=80"; // Dalat pine trees / mountains
            if (p.Contains("nha trang") || p.Contains("khánh hòa"))
                return "https://images.unsplash.com/photo-1540555700478-4be289fbecef?auto=format&fit=crop&w=600&q=80"; // Coastal sea beach
            if (p.Contains("vũng tàu") || p.Contains("bà rịa"))
                return "https://images.unsplash.com/photo-1507525428034-b723cf961d3e?auto=format&fit=crop&w=600&q=80"; // Vung Tau beach
            if (p.Contains("hồ chí minh") || p.Contains("sài gòn"))
                return "https://images.unsplash.com/photo-1583417319070-4a69db38a482?auto=format&fit=crop&w=600&q=80"; // HCM City skyline

            return "https://images.unsplash.com/photo-1544620347-c4fd4a3d5957?auto=format&fit=crop&w=600&q=80"; // Modern bus
        }

        [HttpGet("/Home/Contact")]
        [HttpGet("/Contact")]
        public async Task<IActionResult> Contact()
        {
            var model = new ContactMessage();
            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    model.FullName = user.FullName;
                    model.Email = user.Email ?? string.Empty;
                    model.PhoneNumber = user.PhoneNumber;
                }
            }
            ViewBag.SmtpConfigured = _emailService.IsConfigured();
            return View(model);
        }

        [HttpPost("/Home/Contact")]
        [HttpPost("/Contact")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Contact([Bind("FullName,Email,PhoneNumber,Subject,Message")] ContactMessage input)
        {
            ViewBag.SmtpConfigured = _emailService.IsConfigured();
            if (!ModelState.IsValid)
            {
                return View(input);
            }

            input.FullName = input.FullName.Trim();
            input.Email = input.Email.Trim();
            input.PhoneNumber = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim();
            input.Subject = input.Subject.Trim();
            input.Message = input.Message.Trim();
            input.CreatedAt = DateTime.UtcNow;
            input.UserId = User.Identity?.IsAuthenticated == true ? _userManager.GetUserId(User) : null;

            _context.ContactMessages.Add(input);
            await _context.SaveChangesAsync();

            // Gửi thông tin liên hệ qua email SMTP đến bộ phận hỗ trợ; lưu kết quả để phòng khi SMTP lỗi
            var (sent, error) = await _emailService.SendContactEmailAsync(input);
            input.EmailSent = sent;
            input.EmailError = sent ? null : (error?.Length > 500 ? error[..500] : error);
            await _context.SaveChangesAsync();

            if (sent)
            {
                TempData["SuccessMessage"] = "Cảm ơn bạn! Thông tin liên hệ đã được gửi qua email đến bộ phận hỗ trợ G-Trans.";
            }
            else
            {
                TempData["WarningMessage"] = $"Yêu cầu liên hệ #{input.ContactMessageId} đã được lưu trên hệ thống, nhưng chưa gửi được email SMTP ({error}).";
            }

            return RedirectToAction(nameof(Contact));
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }

    public class HomeViewModel
    {
        public List<string> StartProvinces { get; set; } = new();
        public List<string> EndProvinces { get; set; } = new();
        public List<PopularRouteCardViewModel> PopularRoutes { get; set; } = new();
        public int TotalRoutes { get; set; }
        public int TotalTrips { get; set; }
        public int TotalStations { get; set; }
    }

    public class PopularRouteCardViewModel
    {
        public int RouteId { get; set; }
        public string StartProvince { get; set; } = string.Empty;
        public string EndProvince { get; set; } = string.Empty;
        public int DistanceKm { get; set; }
        public decimal DurationHours { get; set; }
        public decimal MinPrice { get; set; }
        public int TripCount { get; set; }
        public string BusTypeSummary { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
    }
}

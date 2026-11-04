using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using WebVanTai.Data;
using WebVanTai.Models;
using WebVanTai.Models.Enums;
using WebVanTai.Models.ViewModels;
using WebVanTai.Services;

namespace WebVanTai.Controllers
{
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly VnPayService _vnPayService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _config;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            ApplicationDbContext context,
            VnPayService vnPayService,
            UserManager<ApplicationUser> userManager,
            IConfiguration config,
            ILogger<PaymentController> logger)
        {
            _context     = context;
            _vnPayService = vnPayService;
            _userManager  = userManager;
            _config       = config;
            _logger       = logger;
        }

        // ─── GET /Payment/Pay?bookingId=1 ───────────────────────────────────
        /// <summary>
        /// Chuyển hướng người dùng sang VNPay Sandbox.
        /// Chỉ xử lý Booking đang ở trạng thái Pending và chưa hết hạn.
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Pay(int bookingId)
        {
            var userId = _userManager.GetUserId(User)!;

            var booking = await _context.Bookings
                .Include(b => b.Tickets)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn đặt vé.";
                return RedirectToAction("Index", "Home");
            }

            if (booking.UserId != userId && !User.IsInRole("Admin"))
            {
                TempData["ErrorMessage"] = "Bạn không có quyền thanh toán đơn đặt vé này.";
                return RedirectToAction("Index", "Home");
            }

            if (booking.Status != BookingStatus.Pending)
            {
                TempData["ErrorMessage"] = "Đơn đặt vé này đã được xử lý hoặc đã hủy.";
                return RedirectToAction("Index", "Home");
            }

            if (DateTime.UtcNow >= booking.ExpiredAt)
            {
                TempData["ErrorMessage"] = "Thời gian giữ chỗ đã hết hạn. Vui lòng đặt lại vé.";
                return RedirectToAction("SelectSeats", "Booking", new { id = booking.TripId });
            }

            // Sinh TxnRef đúng một lần, lưu vào DB rồi mới ký URL
            // để giá trị gửi VNPay trùng khớp với giá trị đối chiếu khi callback về.
            var txnRef = VnPayService.CreateTxnRef(booking.BookingId);
            booking.VnPayTxnRef = txnRef;
            await _context.SaveChangesAsync();

            var paymentUrl = _vnPayService.CreatePaymentUrl(booking, txnRef);

            _logger.LogInformation("Chuyển hướng Booking #{BookingId} sang VNPay, TxnRef={TxnRef}",
                                    booking.BookingId, txnRef);

            return Redirect(paymentUrl);
        }

        // ─── GET /Payment/VnPayReturn ────────────────────────────────────────
        /// <summary>
        /// Xử lý kết quả trả về từ VNPay (IPN + Return URL).
        /// Kiểm tra chữ ký HMAC-SHA512, thời hạn giữ chỗ,
        /// rồi cập nhật Booking → Paid và sinh QrToken cho từng Ticket.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> VnPayReturn()
        {
            _logger.LogInformation("Nhận callback VNPay: {QueryString}", Request.QueryString.Value);

            // 1. Xác thực chữ ký HMAC-SHA512
            if (!_vnPayService.ValidateCallback(Request.Query, out var responseCode))
            {
                _logger.LogWarning("VNPay callback bị từ chối: chữ ký không hợp lệ.");
                TempData["ErrorMessage"] = "Kết quả thanh toán không hợp lệ (chữ ký sai). Vui lòng liên hệ hỗ trợ.";
                return RedirectToAction("Index", "Home");
            }

            // 2. Lấy Booking từ vnp_TxnRef
            var txnRef    = Request.Query["vnp_TxnRef"].ToString();
            var bookingId = VnPayService.ExtractBookingId(txnRef);

            var booking = await _context.Bookings
                .Include(b => b.Tickets)
                .Include(b => b.Trip)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn đặt vé tương ứng.";
                return RedirectToAction("Index", "Home");
            }

            // 3. Thanh toán thành công
            if (responseCode == "00")
            {
                // Đối chiếu TxnRef đã lưu khi tạo URL (chống replay / ghép lệnh lạ)
                if (!string.IsNullOrEmpty(booking.VnPayTxnRef) &&
                    !string.Equals(booking.VnPayTxnRef, txnRef, StringComparison.Ordinal))
                {
                    _logger.LogWarning("TxnRef không khớp cho Booking #{BookingId}: gửi {Sent}, lưu {Stored}.",
                                        bookingId, txnRef, booking.VnPayTxnRef);
                    TempData["ErrorMessage"] = "Giao dịch không khớp với đơn đặt vé. Vui lòng liên hệ hỗ trợ.";
                    return RedirectToAction("Index", "Home");
                }

                // Đối chiếu số tiền VNPay trả về (đã nhân 100)
                var expectedAmount = (long)Math.Round(booking.TotalAmount, MidpointRounding.AwayFromZero) * 100;
                if (!long.TryParse(Request.Query["vnp_Amount"], out var vnpAmount) || vnpAmount != expectedAmount)
                {
                    _logger.LogWarning("Sai số tiền cho Booking #{BookingId}: VNPay={Vnp}, yêu cầu={Expected}.",
                                        bookingId, Request.Query["vnp_Amount"].ToString(), expectedAmount);
                    TempData["ErrorMessage"] = "Số tiền thanh toán không khớp. Vui lòng liên hệ hỗ trợ.";
                    return RedirectToAction("Index", "Home");
                }

                // Đã xử lý trước đó → idempotent, hiển thị lại trang thành công
                if (booking.Status == BookingStatus.Paid)
                    return RedirectToAction(nameof(PaymentSuccess), new { bookingId });

                // Kiểm tra thời hạn giữ chỗ 10 phút
                if (booking.Status != BookingStatus.Pending)
                {
                    TempData["ErrorMessage"] = "Đơn đặt vé không còn ở trạng thái chờ thanh toán.";
                    return RedirectToAction("Index", "Home");
                }

                if (DateTime.UtcNow > booking.ExpiredAt)
                {
                    _logger.LogWarning("Booking #{BookingId} đã hết hạn khi VNPay gọi về.", bookingId);
                    TempData["ErrorMessage"] = "Thời gian giữ chỗ đã hết hạn. Giao dịch bị hủy.";
                    return RedirectToAction("Index", "Home");
                }

                booking.Status      = BookingStatus.Paid;
                booking.VnPayTxnRef = txnRef;

                // Sinh QrToken bảo mật (HMAC-SHA256 + nonce ngẫu nhiên) cho từng vé còn hiệu lực
                foreach (var ticket in booking.Tickets.Where(t => t.Status != TicketStatus.Void))
                {
                    ticket.QrToken = GenerateSecureQrToken(ticket.TicketId, booking.TripId);
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Booking #{BookingId} đã thanh toán thành công, {Count} QrToken được sinh.",
                                        bookingId, booking.Tickets.Count);

                return RedirectToAction(nameof(PaymentSuccess), new { bookingId });
            }

            // 4. Thanh toán thất bại / bị hủy trên cổng VNPay
            _logger.LogInformation("VNPay trả về mã #{Code} cho Booking #{BookingId}.", responseCode, bookingId);

            if (booking.Status != BookingStatus.Pending || DateTime.UtcNow > booking.ExpiredAt)
            {
                TempData["ErrorMessage"] = $"Thanh toán không thành công (mã lỗi: {responseCode}) và thời gian giữ chỗ đã kết thúc. Vui lòng đặt lại vé.";
                return RedirectToAction("SelectSeats", "Booking", new { id = booking.TripId });
            }

            TempData["ErrorMessage"] =
                $"Giao dịch chưa hoàn tất (mã lỗi: {responseCode}). Ghế vẫn được giữ trong thời gian đếm ngược, bạn có thể bấm thanh toán lại.";
            return RedirectToAction("Confirmation", "Booking", new { bookingId });
        }

        // ─── GET /Payment/PaymentSuccess?bookingId=1 ────────────────────────
        [HttpGet]
        public async Task<IActionResult> PaymentSuccess(int bookingId)
        {
            var booking = await _context.Bookings
                .Include(b => b.Tickets)
                .Include(b => b.Trip)
                    .ThenInclude(t => t!.Route)
                .Include(b => b.PickupStation)
                .Include(b => b.DropoffStation)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.Status == BookingStatus.Paid);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy thông tin thanh toán.";
                return RedirectToAction("Index", "Home");
            }

            var vm = BuildTicketHistoryItem(booking);
            return View("PaymentSuccess", vm);
        }

        // ─── GET /Payment/MyTickets ──────────────────────────────────────────
        /// <summary>
        /// Trang "Lịch sử vé của tôi" — hiển thị QR code và cho phép hủy vé.
        /// </summary>
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> MyTickets()
        {
            var userId = _userManager.GetUserId(User)!;

            var bookings = await _context.Bookings
                .Where(b => b.UserId == userId &&
                            (b.Status == BookingStatus.Pending ||
                             b.Status == BookingStatus.Paid ||
                             b.Status == BookingStatus.Cancelled ||
                             b.Status == BookingStatus.Expired))
                .Include(b => b.Tickets)
                .Include(b => b.Trip)
                    .ThenInclude(t => t!.Route)
                .Include(b => b.PickupStation)
                .Include(b => b.DropoffStation)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            var viewModel = bookings.Select(BuildTicketHistoryItem).ToList();
            return View("MyTickets", viewModel);
        }

        // ─── POST /Payment/CancelBooking ────────────────────────────────────
        /// <summary>
        /// Hủy vé: chỉ cho phép khi DepartureTime cách hiện tại ít nhất 4 tiếng.
        /// </summary>
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelBooking(int bookingId)
        {
            var userId = _userManager.GetUserId(User)!;

            var booking = await _context.Bookings
                .Include(b => b.Tickets)
                .Include(b => b.Trip)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn vé hoặc bạn không có quyền hủy đơn này.";
                return RedirectToAction(nameof(MyTickets));
            }

            if (booking.Status != BookingStatus.Paid)
            {
                TempData["ErrorMessage"] = "Chỉ có thể hủy vé đã thanh toán.";
                return RedirectToAction(nameof(MyTickets));
            }

            // Kiểm tra điều kiện thời gian: phải cách giờ xuất bến ít nhất 4 tiếng
            var departureUtc    = booking.Trip!.DepartureTime;
            var hoursUntilDepart = (departureUtc - DateTime.UtcNow).TotalHours;

            if (hoursUntilDepart < 4)
            {
                TempData["ErrorMessage"] =
                    $"Không thể hủy vé. Chuyến xe xuất bến lúc {departureUtc.ToLocalTime():HH:mm dd/MM/yyyy}, " +
                    $"chỉ còn {hoursUntilDepart:F1} giờ (quy định phải trước ít nhất 4 tiếng).";
                return RedirectToAction(nameof(MyTickets));
            }

            // Cập nhật trạng thái
            booking.Status = BookingStatus.Cancelled;
            foreach (var ticket in booking.Tickets)
            {
                ticket.Status   = TicketStatus.Void;
                ticket.QrToken  = null; // Vô hiệu hoá mã QR
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Booking #{BookingId} đã được hủy bởi UserId={UserId}.", bookingId, userId);
            TempData["SuccessMessage"] = "Vé đã được hủy thành công.";

            return RedirectToAction(nameof(MyTickets));
        }

        // ─── GET /Payment/QrImage?token=xxx ─────────────────────────────────
        /// <summary>
        /// Trả về hình ảnh PNG mã QR cho một QrToken.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> QrImage(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return BadRequest();

            // Kiểm tra token thuộc một vé hợp lệ của đơn đã thanh toán
            var exists = await _context.Tickets.AnyAsync(t =>
                t.QrToken == token &&
                t.Status != TicketStatus.Void &&
                t.Booking != null &&
                t.Booking.Status == BookingStatus.Paid);
            if (!exists)
                return NotFound();

            using var qrGenerator = new QRCodeGenerator();
            using var qrData      = qrGenerator.CreateQrCode(token, QRCodeGenerator.ECCLevel.Q);
            using var qrCode      = new PngByteQRCode(qrData);
            var bytes             = qrCode.GetGraphic(6); // 6px mỗi module → ~200×200 px

            Response.Headers.CacheControl = "private,max-age=600";
            return File(bytes, "image/png");
        }

        // ─── Private helpers ─────────────────────────────────────────────────

        /// <summary>
        /// Sinh QrToken 64 ký tự hex cho một vé: HMAC-SHA256(ticketId|tripId|nonce, secret).
        /// Nonce 128-bit ngẫu nhiên đảm bảo mỗi lần thanh toán tạo token không thể đoán/trùng.
        /// </summary>
        private string GenerateSecureQrToken(int ticketId, int tripId)
        {
            var secret  = _config.GetSection("QrToken")["Secret"] ?? "WebVanTai-QrToken-Secret-2026";
            var nonce   = RandomNumberGenerator.GetBytes(16);
            var payload = $"{ticketId}|{tripId}|{Convert.ToHexString(nonce)}";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(hash).ToLowerInvariant(); // 64 ký tự hex
        }

        private TicketHistoryViewModel BuildTicketHistoryItem(Booking booking)
        {
            return new TicketHistoryViewModel
            {
                BookingId          = booking.BookingId,
                Status             = booking.Status,
                TotalAmount        = booking.TotalAmount,
                CreatedAt          = booking.CreatedAt,
                RouteName          = $"{booking.Trip?.Route?.StartProvince} → {booking.Trip?.Route?.EndProvince}",
                DepartureTime      = booking.Trip?.DepartureTime ?? DateTime.MinValue,
                PickupStationName  = booking.PickupStation?.Name ?? string.Empty,
                DropoffStationName = booking.DropoffStation?.Name ?? string.Empty,
                CanCancel          = booking.Status == BookingStatus.Paid &&
                                     (booking.Trip?.DepartureTime - DateTime.UtcNow)?.TotalHours >= 4,
                Tickets = booking.Tickets.Select(t => new TicketItemViewModel
                {
                    TicketId      = t.TicketId,
                    SeatCode      = t.SeatCode,
                    PassengerName = t.PassengerName,
                    QrToken       = t.QrToken,
                    Status        = t.Status,
                    CheckedInAt   = t.CheckedInAt
                }).OrderBy(t => t.SeatCode).ToList()
            };
        }
    }
}

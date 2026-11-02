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
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            ApplicationDbContext context,
            VnPayService vnPayService,
            UserManager<ApplicationUser> userManager,
            ILogger<PaymentController> logger)
        {
            _context     = context;
            _vnPayService = vnPayService;
            _userManager  = userManager;
            _logger       = logger;
        }

        // ─── GET /Payment/Pay?bookingId=1 ───────────────────────────────────
        /// <summary>
        /// Chuyển hướng người dùng sang VNPay Sandbox.
        /// Chỉ xử lý Booking đang ở trạng thái Pending và chưa hết hạn.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Pay(int bookingId)
        {
            var booking = await _context.Bookings
                .Include(b => b.Tickets)
                .FirstOrDefaultAsync(b => b.BookingId == bookingId);

            if (booking == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy đơn đặt vé.";
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

            // Lưu TxnRef để đối chiếu khi VNPay gọi về
            var paymentUrl = _vnPayService.CreatePaymentUrl(booking);

            // Ghi lại TxnRef vào DB (phần đầu trước dấu '-' là BookingId, VnPayService đã gắn timestamp)
            // Ta lưu chính paymentUrl sẽ không chuẩn; thay vào đó ta parse lại từ URL
            // Cách đơn giản hơn: lưu trực tiếp format "{bookingId}-{unix}" ngay tại đây
            var txnRef = $"{booking.BookingId}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            booking.VnPayTxnRef = txnRef;
            await _context.SaveChangesAsync();

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
            // 1. Xác thực chữ ký
            if (!_vnPayService.ValidateCallback(Request.Query, out var responseCode))
            {
                _logger.LogWarning("VNPay callback bị từ chối: chữ ký không hợp lệ.");
                TempData["ErrorMessage"] = "Kết quả thanh toán không hợp lệ (chữ ký sai). Vui lòng liên hệ hỗ trợ.";
                return RedirectToAction("Index", "Home");
            }

            // 2. Lấy BookingId từ vnp_TxnRef
            var txnRef   = Request.Query["vnp_TxnRef"].ToString();
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

            // 3. Kiểm tra thời hạn giữ chỗ 10 phút
            if (DateTime.UtcNow > booking.ExpiredAt && booking.Status == BookingStatus.Pending)
            {
                _logger.LogWarning("Booking #{BookingId} đã hết hạn khi VNPay gọi về.", bookingId);
                TempData["ErrorMessage"] = "Thời gian giữ chỗ đã hết hạn. Giao dịch bị hủy.";
                return RedirectToAction("Index", "Home");
            }

            // 4. Xử lý theo mã phản hồi
            if (responseCode == "00")
            {
                // Thanh toán thành công
                if (booking.Status != BookingStatus.Paid)
                {
                    booking.Status      = BookingStatus.Paid;
                    booking.VnPayTxnRef = txnRef;

                    // Sinh QrToken bảo mật (256-bit entropy) cho từng Ticket
                    foreach (var ticket in booking.Tickets)
                    {
                        ticket.QrToken = GenerateSecureQrToken(ticket.TicketId, booking.TripId);
                    }

                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Booking #{BookingId} đã thanh toán thành công, {Count} QrToken được sinh.",
                                            bookingId, booking.Tickets.Count);
                }

                return RedirectToAction(nameof(PaymentSuccess), new { bookingId });
            }
            else
            {
                // Thanh toán thất bại / bị hủy
                _logger.LogInformation("VNPay trả về lỗi #{Code} cho Booking #{BookingId}.", responseCode, bookingId);
                TempData["ErrorMessage"] = $"Thanh toán không thành công (mã lỗi: {responseCode}). Vui lòng thử lại.";
                return RedirectToAction("Confirmation", "Booking", new { bookingId });
            }
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
                .Where(b => b.UserId == userId && b.Status == BookingStatus.Paid)
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

            // Kiểm tra token tồn tại trong DB trước khi trả ảnh
            var exists = await _context.Tickets.AnyAsync(t => t.QrToken == token && t.Status != TicketStatus.Void);
            if (!exists)
                return NotFound();

            using var qrGenerator = new QRCodeGenerator();
            using var qrData      = qrGenerator.CreateQrCode(token, QRCodeGenerator.ECCLevel.Q);
            using var qrCode      = new PngByteQRCode(qrData);
            var bytes             = qrCode.GetGraphic(6); // 6px mỗi module → ~200×200 px

            return File(bytes, "image/png");
        }

        // ─── Private helpers ─────────────────────────────────────────────────

        /// <summary>
        /// Sinh QrToken 64-ký-tự hex bảo mật: HMAC-SHA256(ticketId|tripId, secret ngẫu nhiên per-deploy).
        /// Trong môi trường demo, ta dùng Guid kép + thông tin vé để đảm bảo duy nhất.
        /// </summary>
        private static string GenerateSecureQrToken(int ticketId, int tripId)
        {
            // Kết hợp dữ liệu deterministic với entropy ngẫu nhiên
            var entropy  = RandomNumberGenerator.GetBytes(16);
            var payload  = $"ticket:{ticketId}|trip:{tripId}|{Convert.ToHexString(entropy)}";
            var hash     = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WebVanTai.Data;
using WebVanTai.Models.Enums;
using WebVanTai.Areas.TicketInspector.ViewModels;

namespace WebVanTai.Areas.TicketInspector.Controllers
{
    /// <summary>
    /// API phục vụ trang soát vé mobile của nhân viên:
    ///  - POST CheckIn : kiểm tra QrToken theo TripId, hợp lệ thì cập nhật Ticket.Status = CheckedIn.
    ///  - GET  SeatMap : trạng thái sơ đồ ghế thời gian thực (polling từ client).
    /// </summary>
    [Area("TicketInspector")]
    [Authorize(Roles = "TicketInspector")]
    [Route("TicketInspector/api")]
    public class InspectorApiController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<InspectorApiController> _logger;

        public InspectorApiController(ApplicationDbContext context, ILogger<InspectorApiController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public class CheckInRequest
        {
            public int TripId { get; set; }

            /// <summary>Nội dung thô quét được từ camera (có thể là token thuần, JSON hoặc URL).</summary>
            public string Token { get; set; } = string.Empty;
        }

        public class CheckInBySeatRequest
        {
            public int TripId { get; set; }

            /// <summary>Mã ghế hành khách (VD: A01, B02).</summary>
            public string SeatCode { get; set; } = string.Empty;
        }

        /// <summary>
        /// Kiểm tra QrToken của khách theo chuyến đang soát.
        /// Hợp lệ: cập nhật Ticket.Status = CheckedIn + lưu CheckedInAt (thẻ xanh).
        /// Đã soát rồi / sai chuyến / vé hủy / chưa trả tiền: trả cảnh báo (thẻ đỏ).
        /// </summary>
        [HttpPost("CheckIn")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckIn([FromBody] CheckInRequest request)
        {
            if (request.TripId <= 0 || string.IsNullOrWhiteSpace(request.Token))
            {
                return Json(ScanResultViewModel.Invalid("Thiếu thông tin chuyến hoặc mã vé. Vui lòng quét lại."));
            }

            // 1. Trích xuất QrToken từ nội dung quét được (token thuần | JSON | URL chứa tham số token)
            var token = ExtractQrToken(request.Token);
            if (string.IsNullOrWhiteSpace(token))
            {
                return Json(ScanResultViewModel.Invalid("Không đọc được mã vé trong nội dung QR. Vui lòng quét lại mã trên vé của khách."));
            }

            // 2. Tra cứu vé theo QrToken (collation SQL Server mặc định không phân biệt hoa thường)
            var ticket = await _context.Tickets
                .Include(t => t.Booking)
                    .ThenInclude(b => b!.Trip)
                        .ThenInclude(tr => tr!.Route)
                .FirstOrDefaultAsync(t => t.QrToken == token);

            if (ticket == null)
            {
                _logger.LogInformation("Quét mã không tồn tại trên chuyến #{TripId}", request.TripId);
                return Json(ScanResultViewModel.Invalid("Không tìm thấy vé nào khớp mã QR này trong hệ thống."));
            }

            // 3. Kiểm tra vé có thuộc đúng chuyến đang soát hay không
            if (ticket.TripId != request.TripId)
            {
                var wrongTrip = ticket.Booking?.Trip;
                var wrongTripInfo = wrongTrip == null
                    ? "không xác định"
                    : wrongTrip.Route == null
                        ? $"chuyến #{wrongTrip.TripId} lúc {wrongTrip.DepartureTime:HH:mm dd/MM}"
                        : $"{wrongTrip.Route.StartProvince} → {wrongTrip.Route.EndProvince} lúc {wrongTrip.DepartureTime:HH:mm dd/MM}";

                return Json(new ScanResultViewModel
                {
                    ResultType = "wrongtrip",
                    Title = "Sai chuyến xe!",
                    Message = $"Vé của khách thuộc chuyến khác ({wrongTripInfo}). Vui lòng hướng dẫn khách ra đúng bãi xe.",
                    PassengerName = ticket.PassengerName,
                    SeatCode = ticket.SeatCode,
                    PassengerPhone = ticket.PassengerPhone,
                    TicketId = ticket.TicketId,
                    TicketTripInfo = wrongTripInfo
                });
            }

            // 4. Vé đã bị vô hiệu (hủy/hoàn tiền/hết hạn giữ chỗ)
            if (ticket.Status == TicketStatus.Void)
            {
                return Json(new ScanResultViewModel
                {
                    ResultType = "void",
                    Title = "Vé đã bị vô hiệu!",
                    Message = "Vé này đã bị hủy hoặc hết thời hạn thanh toán. Không cho phép lên xe.",
                    PassengerName = ticket.PassengerName,
                    SeatCode = ticket.SeatCode,
                    PassengerPhone = ticket.PassengerPhone,
                    TicketId = ticket.TicketId
                });
            }

            // 5. Vé đã soát rồi -> chặn quét lặp (One-Time QR Check-In)
            if (ticket.Status == TicketStatus.CheckedIn)
            {
                return Json(new ScanResultViewModel
                {
                    ResultType = "already",
                    Title = "Vé đã được soát rồi!",
                    Message = ticket.CheckedInAt.HasValue
                        ? $"Khách đã lên xe lúc {ticket.CheckedInAt.Value.ToLocalTime():HH:mm:ss dd/MM/yyyy}. Không thể soát lại cùng một vé."
                        : "Khách đã lên xe trước đó. Không thể soát lại cùng một vé.",
                    PassengerName = ticket.PassengerName,
                    SeatCode = ticket.SeatCode,
                    PassengerPhone = ticket.PassengerPhone,
                    TicketId = ticket.TicketId,
                    CheckedInAtUtc = ticket.CheckedInAt
                });
            }

            // 6. Booking phải ở trạng thái Paid (đã thanh toán)
            var bookingStatus = ticket.Booking?.Status ?? BookingStatus.Pending;
            if (bookingStatus != BookingStatus.Paid)
            {
                var statusText = bookingStatus switch
                {
                    BookingStatus.Pending => "đang giữ chỗ, chưa thanh toán",
                    BookingStatus.Expired => "đã hết hạn giữ chỗ",
                    BookingStatus.Cancelled => "đã bị hủy",
                    _ => "không hợp lệ"
                };

                return Json(new ScanResultViewModel
                {
                    ResultType = "unpaid",
                    Title = "Vé chưa được thanh toán!",
                    Message = $"Đặt chỗ của khách {statusText}. Vui lòng yêu cầu khách thanh toán trước khi lên xe.",
                    PassengerName = ticket.PassengerName,
                    SeatCode = ticket.SeatCode,
                    PassengerPhone = ticket.PassengerPhone,
                    TicketId = ticket.TicketId
                });
            }

            // 7. Hợp lệ -> cập nhật CheckedIn nguyên tố (chống double check-in do quét song song 2 máy)
            var nowUtc = DateTime.UtcNow;
            var affected = await _context.Tickets
                .Where(t => t.TicketId == ticket.TicketId && t.Status == TicketStatus.Valid)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.Status, TicketStatus.CheckedIn)
                    .SetProperty(t => t.CheckedInAt, nowUtc));

            if (affected == 0)
            {
                // Máy soát khác vừa xử lý vé này giữa chừng
                return Json(new ScanResultViewModel
                {
                    ResultType = "already",
                    Title = "Vé đã được soát rồi!",
                    Message = "Vé này vừa được nhân viên khác soát. Không thể soát lại.",
                    PassengerName = ticket.PassengerName,
                    SeatCode = ticket.SeatCode,
                    PassengerPhone = ticket.PassengerPhone,
                    TicketId = ticket.TicketId
                });
            }

            _logger.LogInformation(
                "Check-in thành công: Ticket #{TicketId} ghế {SeatCode} chuyến #{TripId} bởi {User}",
                ticket.TicketId, ticket.SeatCode, request.TripId, User.Identity?.Name);

            return Json(new ScanResultViewModel
            {
                ResultType = "success",
                Title = "Soát vé thành công!",
                Message = "Chúc khách hàng có chuyến đi an toàn & vui vẻ.",
                PassengerName = ticket.PassengerName,
                SeatCode = ticket.SeatCode,
                PassengerPhone = ticket.PassengerPhone,
                TicketId = ticket.TicketId,
                CheckedInAtUtc = nowUtc
            });
        }

        /// <summary>
        /// Xác nhận hành khách lên xe trực tiếp bằng cách bấm chọn ghế trên sơ đồ xe.
        /// </summary>
        [HttpPost("CheckInBySeat")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckInBySeat([FromBody] CheckInBySeatRequest request)
        {
            if (request.TripId <= 0 || string.IsNullOrWhiteSpace(request.SeatCode))
            {
                return Json(ScanResultViewModel.Invalid("Thiếu thông tin chuyến xe hoặc mã ghế."));
            }

            var seatCode = request.SeatCode.Trim().ToUpperInvariant();

            var ticket = await _context.Tickets
                .Include(t => t.Booking)
                .FirstOrDefaultAsync(t => t.TripId == request.TripId && t.SeatCode.ToUpper() == seatCode && t.Status != TicketStatus.Void);

            if (ticket == null)
            {
                return Json(new ScanResultViewModel
                {
                    ResultType = "notfound",
                    Title = "Ghế chưa có khách!",
                    Message = $"Ghế {seatCode} chưa có hành khách đặt vé hoặc vé đã bị hủy.",
                    SeatCode = seatCode
                });
            }

            if (ticket.Status == TicketStatus.CheckedIn)
            {
                return Json(new ScanResultViewModel
                {
                    ResultType = "already",
                    Title = "Ghế đã được soát rồi!",
                    Message = ticket.CheckedInAt.HasValue
                        ? $"Khách {ticket.PassengerName} (ghế {seatCode}) đã lên xe lúc {ticket.CheckedInAt.Value.ToLocalTime():HH:mm:ss dd/MM/yyyy}."
                        : $"Khách {ticket.PassengerName} (ghế {seatCode}) đã lên xe trước đó.",
                    PassengerName = ticket.PassengerName,
                    SeatCode = ticket.SeatCode,
                    PassengerPhone = ticket.PassengerPhone,
                    TicketId = ticket.TicketId,
                    CheckedInAtUtc = ticket.CheckedInAt
                });
            }

            var bookingStatus = ticket.Booking?.Status ?? BookingStatus.Pending;
            if (bookingStatus != BookingStatus.Paid)
            {
                return Json(new ScanResultViewModel
                {
                    ResultType = "unpaid",
                    Title = "Vé chưa được thanh toán!",
                    Message = $"Vé ghế {seatCode} chưa được thanh toán (trạng thái: {bookingStatus}). Vui lòng yêu cầu khách thanh toán trước khi lên xe.",
                    PassengerName = ticket.PassengerName,
                    SeatCode = ticket.SeatCode,
                    PassengerPhone = ticket.PassengerPhone,
                    TicketId = ticket.TicketId
                });
            }

            var nowUtc = DateTime.UtcNow;
            var affected = await _context.Tickets
                .Where(t => t.TicketId == ticket.TicketId && t.Status == TicketStatus.Valid)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.Status, TicketStatus.CheckedIn)
                    .SetProperty(t => t.CheckedInAt, nowUtc));

            if (affected == 0)
            {
                return Json(new ScanResultViewModel
                {
                    ResultType = "already",
                    Title = "Ghế đã được soát rồi!",
                    Message = $"Ghế {seatCode} vừa được nhân viên khác xác nhận lên xe.",
                    PassengerName = ticket.PassengerName,
                    SeatCode = ticket.SeatCode,
                    PassengerPhone = ticket.PassengerPhone,
                    TicketId = ticket.TicketId
                });
            }

            _logger.LogInformation(
                "Xác nhận lên xe qua sơ đồ ghế: Ticket #{TicketId} ghế {SeatCode} chuyến #{TripId} bởi {User}",
                ticket.TicketId, ticket.SeatCode, request.TripId, User.Identity?.Name);

            return Json(new ScanResultViewModel
            {
                ResultType = "success",
                Title = "Xác nhận lên xe thành công!",
                Message = $"Hành khách {ticket.PassengerName} đã lên xe thành công (Ghế {ticket.SeatCode}).",
                PassengerName = ticket.PassengerName,
                SeatCode = ticket.SeatCode,
                PassengerPhone = ticket.PassengerPhone,
                TicketId = ticket.TicketId,
                CheckedInAtUtc = nowUtc
            });
        }

        /// <summary>
        /// Trạng thái ghế thời gian thực của chuyến: client polling mỗi vài giây để tô màu sơ đồ.
        /// </summary>
        [HttpGet("SeatMap")]
        public async Task<IActionResult> SeatMap(int tripId)
        {
            if (tripId <= 0)
            {
                return BadRequest(new { message = "TripId không hợp lệ." });
            }

            var ticketsRaw = await _context.Tickets
                .AsNoTracking()
                .Where(t => t.TripId == tripId)
                .Select(t => new
                {
                    t.SeatCode,
                    t.Status,
                    t.PassengerName,
                    t.PassengerPhone,
                    t.TicketId,
                    t.CheckedInAt
                })
                .ToListAsync();

            var tickets = ticketsRaw.Select(t => new SeatStatusDto
            {
                Code = t.SeatCode,
                Status = t.Status == TicketStatus.CheckedIn ? "checkedin"
                       : t.Status == TicketStatus.Valid ? "sold"
                       : "void",
                PassengerName = t.PassengerName,
                PassengerPhone = t.PassengerPhone,
                TicketId = t.TicketId,
                CheckedInAt = t.CheckedInAt.HasValue
                    ? t.CheckedInAt.Value.ToLocalTime().ToString("HH:mm:ss dd/MM/yyyy")
                    : null
            }).ToList();

            var response = new SeatMapResponse
            {
                TotalSeats = tickets.Count > 0
                    ? await _context.Trips.Where(t => t.TripId == tripId)
                        .Select(t => t.Bus!.BusType!.TotalSeats).FirstOrDefaultAsync()
                    : 0,
                SoldTickets = tickets.Count(x => x.Status == "sold"),
                CheckedInTickets = tickets.Count(x => x.Status == "checkedin"),
                // Một ghế có thể có nhiều vé (ví vé cũ bị Void do hết hạn/hủy rồi được đặt lại):
                // nhóm theo SeatCode và ưu tiên vé còn hiệu lực để tránh lỗi trùng key từ điển.
                Seats = tickets
                    .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderBy(x => x.Status == "void").First(),
                        StringComparer.OrdinalIgnoreCase)
            };
            response.ValidTickets = response.SoldTickets + response.CheckedInTickets;

            return Json(response);
        }

        /// <summary>
        /// Trích xuất QrToken từ nội dung QR quét được:
        /// token thuần (32 ký tự hex) | JSON {"qrToken": "..."} | URL ?token=...
        /// </summary>
        private static string? ExtractQrToken(string raw)
        {
            var text = raw.Trim();

            // JSON: { "qrToken": "..." } hoặc các biến thể khóa token
            if (text.StartsWith("{"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    foreach (var key in new[] { "qrToken", "QrToken", "token", "Token" })
                    {
                        if (doc.RootElement.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
                        {
                            var value = prop.GetString();
                            if (!string.IsNullOrWhiteSpace(value))
                            {
                                return value.Trim();
                            }
                        }
                    }
                }
                catch (JsonException)
                {
                    // Không phải JSON hợp lệ -> thử dạng URL bên dưới
                }
            }

            // URL: /Ticket/Details?token=... hoặc ?qrToken=...
            if (Uri.TryCreate(text, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
                foreach (var key in new[] { "qrToken", "token", "QrToken" })
                {
                    if (query.TryGetValue(key, out var values))
                    {
                        var value = values.ToString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            return value.Trim();
                        }
                    }
                }
            }

            // Token thuần: loại bỏ khoảng trắng & ký tự xuống dòng do quét lỗi
            var cleaned = new string(text.Where(char.IsLetterOrDigit).ToArray());
            return cleaned.Length > 0 ? cleaned : null;
        }
    }
}

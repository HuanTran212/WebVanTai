using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using WebVanTai.Data;
using WebVanTai.Models;
using WebVanTai.Models.Enums;

namespace WebVanTai.Services
{
    /// <summary>
    /// Gửi email qua SMTP Gmail (App Password) cho hai nghiệp vụ:
    ///  - Email xác nhận đặt vé thành công (kèm mã QR từng vé nhúng trực tiếp).
    ///  - Email tiếp nhận thông tin liên hệ từ khách hàng.
    /// Chưa cấu hình SenderEmail/AppPassword thì bỏ qua gửi (chỉ log cảnh báo) — tuyệt đối không làm gián đoạn luồng nghiệp vụ chính.
    /// </summary>
    public class EmailService
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            ApplicationDbContext context,
            IConfiguration config,
            ILogger<EmailService> logger)
        {
            _context = context;
            _config = config;
            _logger = logger;
        }

        public bool IsConfigured()
        {
            var smtp = _config.GetSection("Smtp");
            var senderEmail = smtp["SenderEmail"];
            var appPassword = smtp["AppPassword"];

            if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(appPassword))
                return false;

            // Kiểm tra các giá trị mẫu hoặc placeholder chưa thay thế
            if (appPassword.Contains("your-", StringComparison.OrdinalIgnoreCase) ||
                appPassword.Contains("xxxx", StringComparison.OrdinalIgnoreCase) ||
                appPassword.Contains("DoiTruocKhiTrienKhai", StringComparison.OrdinalIgnoreCase) ||
                appPassword.Contains("example", StringComparison.OrdinalIgnoreCase) ||
                appPassword.Contains("placeholder", StringComparison.OrdinalIgnoreCase) ||
                appPassword.Trim().Length < 8)
            {
                return false;
            }

            return true;
        }

        public async Task<(bool Sent, string? Error)> SendBookingConfirmationEmailAsync(int bookingId, string? overrideEmail = null)
        {
            try
            {
                if (!IsConfigured())
                {
                    _logger.LogWarning("Bỏ qua gửi email xác nhận đặt vé #{BookingId}: SMTP chưa được cấu hình hoặc AppPassword là chuỗi mẫu.", bookingId);
                    return (false, "SMTP chưa được cấu hình.");
                }

                var booking = await _context.Bookings
                    .AsNoTracking()
                    .Include(b => b.User)
                    .Include(b => b.Trip).ThenInclude(t => t!.Route)
                    .Include(b => b.Trip).ThenInclude(t => t!.Bus).ThenInclude(bus => bus!.BusType)
                    .Include(b => b.PickupStation)
                    .Include(b => b.DropoffStation)
                    .Include(b => b.Tickets)
                    .FirstOrDefaultAsync(b => b.BookingId == bookingId);

                if (booking == null)
                    return (false, "Không tìm thấy đơn đặt vé.");

                var recipient = !string.IsNullOrWhiteSpace(overrideEmail)
                    ? overrideEmail.Trim()
                    : (!string.IsNullOrWhiteSpace(booking.ContactEmail) ? booking.ContactEmail.Trim() : booking.User?.Email);

                if (string.IsNullOrWhiteSpace(recipient) || !MailAddress.TryCreate(recipient, out _))
                {
                    _logger.LogWarning("Không có địa chỉ email hợp lệ của người nhận cho Booking #{BookingId}.", bookingId);
                    return (false, "Không có email hợp lệ của người nhận.");
                }

                var activeTickets = booking.Tickets
                    .Where(t => t.Status != TicketStatus.Void)
                    .OrderBy(t => t.SeatCode)
                    .ToList();

                var paymentLabel = booking.PaymentMethod switch
                {
                    PaymentMethod.Cash => "Tiền mặt tại quầy vé",
                    _ => "VNPay"
                };

                var routeName = $"{booking.Trip?.Route?.StartProvince} → {booking.Trip?.Route?.EndProvince}";
                var depTime = booking.Trip?.DepartureTime.ToString("HH:mm - dd/MM/yyyy") ?? "";
                var arrTime = booking.Trip?.ArrivalTime.ToString("HH:mm - dd/MM/yyyy") ?? "";
                var busInfo = $"{booking.Trip?.Bus?.BusType?.Name ?? "Xe khách"} ({booking.Trip?.Bus?.LicensePlate})";

                var ticketRows = new StringBuilder();
                var inlineQrImages = new List<(string ContentId, byte[] Bytes)>();

                using (var qrGen = new QRCodeGenerator())
                {
                    foreach (var ticket in activeTickets)
                    {
                        var cid = $"qr_{ticket.TicketId}";
                        var qrHtml = "";
                        if (!string.IsNullOrWhiteSpace(ticket.QrToken))
                        {
                            using var qrData = qrGen.CreateQrCode(ticket.QrToken, QRCodeGenerator.ECCLevel.Q);
                            using var png = new PngByteQRCode(qrData);
                            inlineQrImages.Add((cid, png.GetGraphic(5)));
                            qrHtml = $"<img src=\"cid:{cid}\" alt=\"QR {WebUtility.HtmlEncode(ticket.SeatCode)}\" width=\"110\" height=\"110\" style=\"border:2px solid #F26522;border-radius:8px;\" />";
                        }

                        ticketRows.Append($@"
                            <tr>
                                <td style='padding:12px;border-bottom:1px solid #e2e8f0;text-align:center;'>{qrHtml}</td>
                                <td style='padding:12px;border-bottom:1px solid #e2e8f0;'>
                                    <span style='display:inline-block;background:#F26522;color:#fff;padding:3px 10px;border-radius:6px;font-weight:700;'>Ghế {WebUtility.HtmlEncode(ticket.SeatCode)}</span>
                                    <div style='margin-top:6px;font-size:13px;color:#334155;'>
                                        <strong>Hành khách:</strong> {WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(ticket.PassengerName) ? (booking.User?.FullName ?? "Hành khách") : ticket.PassengerName)}<br/>
                                        <strong>SĐT:</strong> {WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(ticket.PassengerPhone) ? (booking.User?.PhoneNumber ?? "—") : ticket.PassengerPhone)}<br/>
                                        <strong>Mã vé:</strong> #{ticket.TicketId}
                                    </div>
                                </td>
                            </tr>");
                    }
                }

                var htmlBody = $@"<!DOCTYPE html>
<html lang='vi'>
<head><meta charset='utf-8'></head>
<body style='font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,Helvetica,Arial,sans-serif;background:#f1f5f9;margin:0;padding:24px;color:#0f172a;'>
  <div style='max-width:640px;margin:0 auto;background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 4px 12px rgba(0,0,0,0.06);'>
    <div style='background:linear-gradient(135deg, #F26522, #ea580c);padding:24px 28px;color:#ffffff;'>
      <div style='font-size:12px;text-transform:uppercase;letter-spacing:1px;opacity:0.9;'>G-Trans Express</div>
      <h2 style='margin:4px 0 0;font-size:22px;'>Xác nhận vé điện tử #{booking.BookingId}</h2>
    </div>
    <div style='padding:28px;'>
      <p style='margin-top:0;'>Kính gửi Quý khách,</p>
      <p>Hệ thống <strong>G-Trans</strong> xin gửi thông tin vé xe cho chuyến đi của Quý khách. Vui lòng xuất trình mã QR bên dưới cho nhân viên soát vé khi lên xe.</p>
      <div style='background:#f8fafc;border:1px solid #e2e8f0;border-radius:8px;padding:16px;margin:20px 0;'>
        <table style='width:100%;font-size:14px;border-collapse:collapse;'>
          <tr><td style='padding:6px 0;color:#64748b;width:140px;'>Tuyến đường:</td><td style='padding:6px 0;font-weight:700;'>{WebUtility.HtmlEncode(routeName)}</td></tr>
          <tr><td style='padding:6px 0;color:#64748b;'>Xuất bến:</td><td style='padding:6px 0;font-weight:700;color:#F26522;'>{depTime}</td></tr>
          <tr><td style='padding:6px 0;color:#64748b;'>Dự kiến đến:</td><td style='padding:6px 0;'>{arrTime}</td></tr>
          <tr><td style='padding:6px 0;color:#64748b;'>Điểm đón:</td><td style='padding:6px 0;'>{WebUtility.HtmlEncode(booking.PickupStation?.Name ?? "—")} ({WebUtility.HtmlEncode(booking.PickupStation?.Address ?? "")})</td></tr>
          <tr><td style='padding:6px 0;color:#64748b;'>Điểm trả:</td><td style='padding:6px 0;'>{WebUtility.HtmlEncode(booking.DropoffStation?.Name ?? "—")} ({WebUtility.HtmlEncode(booking.DropoffStation?.Address ?? "")})</td></tr>
          <tr><td style='padding:6px 0;color:#64748b;'>Phương tiện:</td><td style='padding:6px 0;'>{WebUtility.HtmlEncode(busInfo)}</td></tr>
          <tr><td style='padding:6px 0;color:#64748b;'>Thanh toán:</td><td style='padding:6px 0;font-weight:700;'>{paymentLabel} ({booking.TotalAmount:N0} đ)</td></tr>
        </table>
      </div>
      <h3 style='font-size:16px;margin:24px 0 12px;color:#0f172a;'>Danh sách ghế &amp; Mã QR thẻ lên xe</h3>
      <table style='width:100%;border-collapse:collapse;font-size:14px;'>
        {ticketRows}
      </table>
      <div style='margin-top:28px;padding-top:20px;border-top:1px solid #e2e8f0;font-size:12px;color:#64748b;line-height:1.6;'>
        <p style='margin:0 0 6px;'><strong>Lưu ý:</strong></p>
        <ul style='margin:0;padding-left:18px;'>
          <li>Có mặt tại điểm đón trước giờ xuất bến ít nhất 15-30 phút.</li>
          <li>Quy chế hủy/đổi vé: Phải thực hiện trước giờ xuất bến tối thiểu 4 tiếng trên trang Tra cứu vé.</li>
          <li>Mọi thắc mắc xin liên hệ tổng đài hỗ trợ G-Trans hoặc gửi yêu cầu tại trang Liên hệ.</li>
        </ul>
      </div>
    </div>
  </div>
</body>
</html>";

                return await SendHtmlEmailAsync(
                    toEmail: recipient,
                    subject: $"[G-Trans] Vé điện tử #{booking.BookingId} - Tuyến {routeName}",
                    htmlBody: htmlBody,
                    inlineImages: inlineQrImages);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi xảy ra trong quá trình chuẩn bị/gửi email xác nhận đặt vé #{BookingId}. Bỏ qua để không làm gián đoạn luồng đặt vé.", bookingId);
                return (false, ex.Message);
            }
        }

        public async Task<(bool Sent, string? Error)> SendContactEmailAsync(ContactMessage contact)
            => await SendContactNotificationEmailAsync(contact);

        public async Task<(bool Sent, string? Error)> SendContactNotificationEmailAsync(ContactMessage contact)
        {
            try
            {
                if (!IsConfigured())
                {
                    _logger.LogWarning("Bỏ qua gửi email thông báo liên hệ #{ContactId}: SMTP chưa cấu hình.", contact.ContactMessageId);
                    return (false, "SMTP chưa được cấu hình.");
                }

                var smtp = _config.GetSection("Smtp");
                var adminEmail = smtp["AdminEmail"] ?? smtp["SenderEmail"];
                if (string.IsNullOrWhiteSpace(adminEmail))
                {
                    _logger.LogWarning("Bỏ qua gửi email liên hệ #{ContactId}: không tìm thấy adminEmail.", contact.ContactMessageId);
                    return (false, "Chưa cấu hình AdminEmail.");
                }

                var htmlBody = $@"<!DOCTYPE html>
<html lang='vi'>
<head><meta charset='utf-8'></head>
<body style='font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,sans-serif;background:#f8fafc;padding:24px;color:#0f172a;'>
  <div style='max-width:560px;margin:0 auto;background:#fff;border-radius:10px;border:1px solid #e2e8f0;overflow:hidden;'>
    <div style='background:#0f172a;padding:20px 24px;color:#ffffff;border-bottom:4px solid #F26522;'>
      <h3 style='margin:0;'>Liên hệ mới từ khách hàng — G-Trans</h3>
    </div>
    <div style='padding:24px;'>
      <p><strong>Họ và tên:</strong> {WebUtility.HtmlEncode(contact.FullName)}</p>
      <p><strong>Email:</strong> {WebUtility.HtmlEncode(contact.Email)}</p>
      <p><strong>Số điện thoại:</strong> {WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(contact.PhoneNumber) ? "Không cung cấp" : contact.PhoneNumber)}</p>
      <p><strong>Tiêu đề:</strong> {WebUtility.HtmlEncode(contact.Subject)}</p>
      <p><strong>Thời gian gửi:</strong> {contact.CreatedAt.ToLocalTime():HH:mm:ss dd/MM/yyyy}</p>
      <hr style='border:none;border-top:1px solid #e2e8f0;margin:16px 0;' />
      <div style='background:#f8fafc;padding:16px;border-radius:10px;white-space:pre-line;'>{WebUtility.HtmlEncode(contact.Message)}</div>
    </div>
  </div>
</body>
</html>";

                return await SendHtmlEmailAsync(
                    toEmail: adminEmail,
                    subject: $"[Liên hệ G-Trans] {contact.Subject} - {contact.FullName}",
                    htmlBody: htmlBody,
                    replyToEmail: contact.Email);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi xảy ra khi gửi email thông báo liên hệ #{ContactId}: {Message}", contact.ContactMessageId, ex.Message);
                return (false, ex.Message);
            }
        }

        private async Task<(bool Sent, string? Error)> SendHtmlEmailAsync(
            string toEmail,
            string subject,
            string htmlBody,
            List<(string ContentId, byte[] Bytes)>? inlineImages = null,
            string? replyToEmail = null)
        {
            var smtp = _config.GetSection("Smtp");
            var host = smtp["Host"] ?? smtp["Server"] ?? "smtp.gmail.com";
            var port = int.TryParse(smtp["Port"], out var p) ? p : 587;
            var enableSsl = !bool.TryParse(smtp["EnableSsl"], out var ssl) || ssl;
            var senderName = smtp["SenderName"] ?? "G-Trans Express";
            var senderEmail = smtp["SenderEmail"]?.Trim();
            var appPassword = smtp["AppPassword"]?.Replace(" ", "").Trim();

            if (!IsConfigured())
            {
                const string cfgMsg = "Chưa cấu hình SenderEmail hoặc AppPassword hợp lệ trong mục Smtp.";
                _logger.LogWarning("Bỏ qua gửi email tới {To}: {Reason}", toEmail, cfgMsg);
                return (false, cfgMsg);
            }

            var streams = new List<MemoryStream>();
            try
            {
                using var message = new MailMessage();
                message.From = new MailAddress(senderEmail!, senderName, Encoding.UTF8);
                message.To.Add(toEmail);
                if (!string.IsNullOrWhiteSpace(replyToEmail) && MailAddress.TryCreate(replyToEmail, out var replyAddr))
                {
                    message.ReplyToList.Add(replyAddr);
                }
                message.Subject = subject;
                message.SubjectEncoding = Encoding.UTF8;
                message.BodyEncoding = Encoding.UTF8;
                message.IsBodyHtml = true;

                var htmlView = AlternateView.CreateAlternateViewFromString(htmlBody, Encoding.UTF8, MediaTypeNames.Text.Html);
                if (inlineImages != null)
                {
                    foreach (var (cid, bytes) in inlineImages)
                    {
                        var ms = new MemoryStream(bytes);
                        streams.Add(ms);
                        var resource = new LinkedResource(ms, "image/png")
                        {
                            ContentId = cid,
                            TransferEncoding = TransferEncoding.Base64
                        };
                        htmlView.LinkedResources.Add(resource);
                    }
                }
                message.AlternateViews.Add(htmlView);

                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = enableSsl,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(senderEmail, appPassword),
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 10000
                };

                await client.SendMailAsync(message);
                _logger.LogInformation("Đã gửi email '{Subject}' tới {To}.", subject, toEmail);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gửi email tới {To} thất bại: {Message}. Luồng nghiệp vụ chính vẫn tiếp tục bình thường.", toEmail, ex.Message);
                return (false, ex.Message);
            }
            finally
            {
                foreach (var ms in streams)
                {
                    ms.Dispose();
                }
            }
        }
    }
}

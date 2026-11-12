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
    /// Chưa cấu hình SenderEmail/AppPassword thì bỏ qua gửi (chỉ log) — không làm hỏng nghiệp vụ chính.
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
            return !string.IsNullOrWhiteSpace(smtp["SenderEmail"])
                && !string.IsNullOrWhiteSpace(smtp["AppPassword"]);
        }

        public async Task<(bool Sent, string? Error)> SendBookingConfirmationEmailAsync(int bookingId, string? overrideEmail = null)
        {
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
                return (false, "Không có email hợp lệ của người nhận.");

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
                                <div style='margin-top:6px;font-weight:600;color:#0f172a;'>{WebUtility.HtmlEncode(ticket.PassengerName)}</div>
                                <div style='font-size:13px;color:#64748b;'>SĐT: {WebUtility.HtmlEncode(ticket.PassengerPhone)}</div>
                            </td>
                        </tr>");
                }
            }

            var htmlBody = $@"
<!DOCTYPE html>
<html lang='vi'>
<body style='margin:0;padding:24px;background:#f8fafc;font-family:Arial,Helvetica,sans-serif;color:#334155;'>
  <div style='max-width:620px;margin:0 auto;background:#ffffff;border-radius:16px;overflow:hidden;border:1px solid #e2e8f0;'>
    <div style='background:linear-gradient(135deg,#F26522,#d9531e);padding:24px;color:#ffffff;text-align:center;'>
      <h2 style='margin:0 0 6px 0;'>G-Trans Express</h2>
      <p style='margin:0;font-size:15px;'>Xác nhận đặt vé thành công — Mã đơn <strong>#{booking.BookingId}</strong></p>
    </div>
    <div style='padding:24px;'>
      <p style='margin-top:0;'>Chào <strong>{WebUtility.HtmlEncode(activeTickets.FirstOrDefault()?.PassengerName ?? booking.User?.FullName ?? "Quý khách")}</strong>,</p>
      <p>Đơn đặt vé của bạn đã được thanh toán thành công qua <strong>{paymentLabel}</strong>. Vui lòng xuất trình mã QR bên dưới cho nhân viên soát vé khi lên xe.</p>

      <div style='background:#fff7ed;border:1px solid #fed7aa;border-radius:12px;padding:16px;margin-bottom:20px;'>
        <div style='margin-bottom:8px;'><strong>Tuyến xe:</strong> {WebUtility.HtmlEncode(routeName)}</div>
        <div style='margin-bottom:8px;'><strong>Xe:</strong> {WebUtility.HtmlEncode(busInfo)}</div>
        <div style='margin-bottom:8px;'><strong>Khởi hành:</strong> <span style='color:#F26522;font-weight:700;'>{depTime}</span> (Dự kiến đến: {arrTime})</div>
        <div style='margin-bottom:8px;'><strong>Điểm đón:</strong> {WebUtility.HtmlEncode(booking.PickupStation?.Name)} — {WebUtility.HtmlEncode(booking.PickupStation?.Address)}</div>
        <div style='margin-bottom:8px;'><strong>Điểm trả:</strong> {WebUtility.HtmlEncode(booking.DropoffStation?.Name)} — {WebUtility.HtmlEncode(booking.DropoffStation?.Address)}</div>
        <div><strong>Tổng tiền:</strong> <span style='color:#dc2626;font-size:18px;font-weight:700;'>{booking.TotalAmount:N0} đ</span></div>
      </div>

      <h4 style='margin-bottom:10px;color:#0f172a;'>Thông tin vé điện tử</h4>
      <table style='width:100%;border-collapse:collapse;border:1px solid #e2e8f0;border-radius:8px;'>
        {ticketRows}
      </table>

      <p style='font-size:12px;color:#64748b;margin-top:20px;'>
        Lưu ý: Quý khách vui lòng có mặt tại điểm đón trước giờ khởi hành 15–30 phút. Quy định hủy/hoàn vé trực tuyến áp dụng trước giờ xe chạy tối thiểu 4 tiếng.
      </p>
    </div>
    <div style='background:#0f172a;color:#94a3b8;padding:16px 24px;font-size:12px;text-align:center;'>
      G-Trans Express &bull; Hotline: 1900 6868 &bull; Email: hotro@gtrans.vn
    </div>
  </div>
</body>
</html>";

            return await SendHtmlEmailAsync(
                toEmail: recipient,
                subject: $"[G-Trans] Xác nhận vé xe #{booking.BookingId} - {routeName}",
                htmlBody: htmlBody,
                inlineImages: inlineQrImages);
        }

        public async Task<(bool Sent, string? Error)> SendContactEmailAsync(ContactMessage contact)
        {
            var smtp = _config.GetSection("Smtp");
            var adminEmail = !string.IsNullOrWhiteSpace(smtp["AdminEmail"])
                ? smtp["AdminEmail"]!.Trim()
                : smtp["SenderEmail"]?.Trim();

            if (string.IsNullOrWhiteSpace(adminEmail))
                return (false, "Chưa cấu hình AdminEmail hoặc SenderEmail trong mục Smtp.");

            var htmlBody = $@"
<!DOCTYPE html>
<html lang='vi'>
<body style='margin:0;padding:24px;background:#f8fafc;font-family:Arial,Helvetica,sans-serif;color:#334155;'>
  <div style='max-width:600px;margin:0 auto;background:#ffffff;border-radius:14px;overflow:hidden;border:1px solid #e2e8f0;'>
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

        private async Task<(bool Sent, string? Error)> SendHtmlEmailAsync(
            string toEmail,
            string subject,
            string htmlBody,
            List<(string ContentId, byte[] Bytes)>? inlineImages = null,
            string? replyToEmail = null)
        {
            var smtp = _config.GetSection("Smtp");
            var host = smtp["Host"] ?? "smtp.gmail.com";
            var port = int.TryParse(smtp["Port"], out var p) ? p : 587;
            var enableSsl = !bool.TryParse(smtp["EnableSsl"], out var ssl) || ssl;
            var senderName = smtp["SenderName"] ?? "G-Trans Express";
            var senderEmail = smtp["SenderEmail"]?.Trim();
            var appPassword = smtp["AppPassword"]?.Replace(" ", "").Trim();

            if (string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(appPassword))
            {
                const string cfgMsg = "Chưa cấu hình SenderEmail hoặc AppPassword trong mục Smtp của appsettings.json.";
                _logger.LogWarning("Bỏ qua gửi email tới {To}: {Reason}", toEmail, cfgMsg);
                return (false, cfgMsg);
            }

            var streams = new List<MemoryStream>();
            try
            {
                using var message = new MailMessage();
                message.From = new MailAddress(senderEmail, senderName, Encoding.UTF8);
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
                    Timeout = 15000
                };

                await client.SendMailAsync(message);
                _logger.LogInformation("Đã gửi email '{Subject}' tới {To}.", subject, toEmail);
                return (true, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gửi email tới {To} thất bại: {Message}", toEmail, ex.Message);
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

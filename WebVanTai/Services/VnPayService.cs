using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using WebVanTai.Models;

namespace WebVanTai.Services
{
    /// <summary>
    /// Dịch vụ tích hợp cổng thanh toán VNPay Sandbox.
    /// Thuật toán ký: HMAC-SHA512 trên chuỗi query đã sắp xếp thứ tự key (Ordinal),
    /// value được URL-encode kiểu PHP urlencode (hex HOA) đúng chuẩn VNPay.
    /// </summary>
    public class VnPayService
    {
        private readonly IConfiguration _config;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public VnPayService(IConfiguration config, IHttpContextAccessor httpContextAccessor)
        {
            _config = config;
            _httpContextAccessor = httpContextAccessor;
        }

        // ─── Tạo URL thanh toán ──────────────────────────────────────────────

        /// <summary>
        /// Sinh mã giao dịch duy nhất cho một lần thanh toán: "{bookingId}-{yyyyMMddHHmmss}".
        /// Phần trước dấu '-' là BookingId để đối chiếu khi VNPay gọi về.
        /// </summary>
        public static string CreateTxnRef(int bookingId)
            => $"{bookingId}-{DateTime.UtcNow:yyyyMMddHHmmss}";

        /// <summary>
        /// Tạo URL chuyển hướng sang VNPay Sandbox từ một Booking đang Pending.
        /// <paramref name="txnRef"/> do caller sinh và lưu vào Booking trước khi gọi,
        /// bảo đảm giá trị gửi đi trùng khớp với giá trị đối chiếu khi callback về.
        /// </summary>
        public string CreatePaymentUrl(Booking booking, string txnRef)
        {
            if (string.IsNullOrWhiteSpace(txnRef))
                throw new ArgumentException("vnp_TxnRef không được để trống.", nameof(txnRef));

            var cfg = _config.GetSection("VnPay");
            var tmnCode    = cfg["TmnCode"] ?? "DEMOV210";
            var version    = cfg["Version"] ?? "2.1.0";
            var command    = cfg["Command"] ?? "pay";
            var currCode   = cfg["CurrCode"] ?? "VND";
            var locale     = cfg["Locale"] ?? "vn";
            var orderType  = cfg["OrderType"] ?? "other";
            var returnUrl  = cfg["ReturnUrl"] ?? "https://localhost:7164/Payment/VnPayReturn";
            var baseUrl    = cfg["BaseUrl"] ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
            var hashSecret = cfg["HashSecret"] ?? "RAOEXHYVSDDIIENYWSLDIIZTBI";

            // VNPay yêu cầu Amount là số nguyên, đã nhân 100 (VND không có phần thập phân)
            var amount = ((long)Math.Round(booking.TotalAmount, MidpointRounding.AwayFromZero) * 100)
                         .ToString(CultureInfo.InvariantCulture);

            var createDate = ToVnPayDate(DateTime.UtcNow);
            var expireDate = ToVnPayDate(booking.ExpiredAt);

            // SortedDictionary + StringComparer.Ordinal ⇒ thứ tự key alphabet chuẩn VNPay
            var vnpParams = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["vnp_Version"]    = version,
                ["vnp_Command"]    = command,
                ["vnp_TmnCode"]    = tmnCode,
                ["vnp_Amount"]     = amount,
                ["vnp_CurrCode"]   = currCode,
                ["vnp_TxnRef"]     = txnRef,
                ["vnp_OrderInfo"]  = $"Thanh toan ve xe BookingId {booking.BookingId}",
                ["vnp_OrderType"]  = orderType,
                ["vnp_Locale"]     = locale,
                ["vnp_ReturnUrl"]  = returnUrl,
                ["vnp_IpAddr"]     = GetClientIpAddress(),
                ["vnp_CreateDate"] = createDate,
                ["vnp_ExpireDate"] = expireDate,
            };

            // Chuỗi dùng để ký chính là query string (value đã URL-encode) — chuẩn VNPay
            var queryString = BuildQuery(vnpParams);
            var secureHash  = HmacSha512(hashSecret, queryString);
            return $"{baseUrl}?{queryString}&vnp_SecureHash={secureHash}";
        }

        // ─── Xác thực chữ ký VNPay trả về ──────────────────────────────────

        /// <summary>
        /// Kiểm tra vnp_SecureHash trong callback từ VNPay (Return URL / IPN).
        /// Loại bỏ vnp_SecureHash &amp; vnp_SecureHashType, sắp xếp lại tham số,
        /// ký lại bằng HashSecret và so sánh không phân biệt hoa thường.
        /// </summary>
        public bool ValidateCallback(IQueryCollection query, out string responseCode)
        {
            var cfg = _config.GetSection("VnPay");
            var hashSecret = cfg["HashSecret"] ?? "RAOEXHYVSDDIIENYWSLDIIZTBI";

            responseCode = query["vnp_ResponseCode"].ToString();

            var vnpSecureHash = query["vnp_SecureHash"].ToString();
            if (string.IsNullOrEmpty(vnpSecureHash))
                return false;

            var vnpParams = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var (key, value) in query)
            {
                if (key.StartsWith("vnp_", StringComparison.Ordinal) &&
                    key != "vnp_SecureHash" &&
                    key != "vnp_SecureHashType" &&
                    !string.IsNullOrEmpty(value))
                {
                    vnpParams[key] = value!;
                }
            }

            var rawHash   = BuildQuery(vnpParams);
            var checkHash = HmacSha512(hashSecret, rawHash);

            return CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(vnpSecureHash.ToLowerInvariant()),
                Encoding.ASCII.GetBytes(checkHash));
        }

        // ─── Helper: lấy BookingId từ TxnRef ───────────────────────────────

        /// <summary>
        /// Lấy BookingId từ vnp_TxnRef có dạng "{bookingId}-{timestamp}".
        /// </summary>
        public static int ExtractBookingId(string txnRef)
        {
            if (string.IsNullOrWhiteSpace(txnRef)) return 0;
            var parts = txnRef.Split('-', 2);
            return int.TryParse(parts[0], out var id) ? id : 0;
        }

        // ─── Private helpers ─────────────────────────────────────────────────

        /// <summary>
        /// Tạo chuỗi "key1=value1&amp;key2=value2" theo thứ tự key đã sắp xếp,
        /// value được URL-encode theo chuẩn PHP urlencode của VNPay (space → '+',
        /// hex %XX viết HOA — khác với HttpUtility.UrlEncode viết thường, gây "Sai chữ ký").
        /// </summary>
        private static string BuildQuery(SortedDictionary<string, string> parameters)
        {
            var sb = new StringBuilder();
            foreach (var (key, value) in parameters)
            {
                if (string.IsNullOrEmpty(value)) continue;

                if (sb.Length > 0) sb.Append('&');
                sb.Append(key);
                sb.Append('=');
                sb.Append(PhpUrlEncode(value));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Tương đương hàm urlencode() của PHP: giữ nguyên A-Z a-z 0-9 - _ .,
        /// space thành '+', còn lại thành %XX (hex HOA).
        /// </summary>
        private static string PhpUrlEncode(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var sb = new StringBuilder(bytes.Length);
            foreach (var b in bytes)
            {
                if (b is >= (byte)'a' and <= (byte)'z' or
                    >= (byte)'A' and <= (byte)'Z' or
                    >= (byte)'0' and <= (byte)'9' or
                    (byte)'-' or (byte)'_' or (byte)'.')
                {
                    sb.Append((char)b);
                }
                else if (b == (byte)' ')
                {
                    sb.Append('+');
                }
                else
                {
                    sb.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
                }
            }
            return sb.ToString();
        }

        /// <summary>HMAC-SHA512 chuẩn VNPay — trả về chuỗi hex lowercase.</summary>
        private static string HmacSha512(string key, string data)
        {
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        /// <summary>Định dạng thời gian yyyyMMddHHmmss theo múi giờ Việt Nam (UTC+7).</summary>
        private static string ToVnPayDate(DateTime utcTime)
        {
            var vnTime = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(utcTime, DateTimeKind.Utc), GetVietnamTimeZone());
            return vnTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        }

        private static TimeZoneInfo GetVietnamTimeZone()
        {
            foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }
            return TimeZoneInfo.CreateCustomTimeZone("UTC+7", TimeSpan.FromHours(7), "VN", "VN");
        }

        private string GetClientIpAddress()
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx == null) return "127.0.0.1";

            var forwarded = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded))
                return forwarded.Split(',')[0].Trim();

            var ip = ctx.Connection.RemoteIpAddress;
            if (ip == null) return "127.0.0.1";
            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
            // VNPay Sandbox chỉ chấp nhận IPv4; localhost qua Kestrel thường trả về ::1
            return ip.Equals(System.Net.IPAddress.IPv6Loopback) ? "127.0.0.1" : ip.ToString();
        }
    }
}

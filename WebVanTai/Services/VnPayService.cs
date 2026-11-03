using System.Net;
using System.Security.Cryptography;
using System.Text;
using WebVanTai.Models;

namespace WebVanTai.Services
{
    /// <summary>
    /// Dịch vụ tích hợp cổng thanh toán VNPay Sandbox.
    /// Thuật toán ký: HMAC-SHA512 trên chuỗi query đã sắp xếp thứ tự key.
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
        /// Tạo URL chuyển hướng sang VNPay Sandbox từ một Booking đang Pending.
        /// </summary>
        public string CreatePaymentUrl(Booking booking)
        {
            var cfg = _config.GetSection("VnPay");
            var tmnCode   = cfg["TmnCode"]!;
            var version   = cfg["Version"]!;
            var command   = cfg["Command"]!;
            var currCode  = cfg["CurrCode"]!;
            var locale    = cfg["Locale"]!;
            var returnUrl = cfg["ReturnUrl"]!;
            var baseUrl   = cfg["BaseUrl"]!;
            var hashSecret = cfg["HashSecret"]!;

            // vnp_TxnRef = mã đơn hàng phải là duy nhất mỗi lần gọi
            var txnRef = $"{booking.BookingId}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";

            // Số tiền nhân 100 (VNPay yêu cầu không có dấu thập phân)
            var amount = ((long)(booking.TotalAmount * 100)).ToString();

            // Lấy IP thực của client
            var ipAddr = GetClientIpAddress();

            // Thời gian tạo đơn (múi giờ Việt Nam +07:00)
            var vnTimeZone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var createDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vnTimeZone)
                                         .ToString("yyyyMMddHHmmss");

            // ── Xây dựng tập tham số (thứ tự alphabet không quan trọng,
            //    ta sẽ sắp xếp khi ký) ──────────────────────────────────
            var vnpParams = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["vnp_Version"]    = version,
                ["vnp_Command"]    = command,
                ["vnp_TmnCode"]    = tmnCode,
                ["vnp_Amount"]     = amount,
                ["vnp_CurrCode"]   = currCode,
                ["vnp_TxnRef"]     = txnRef,
                ["vnp_OrderInfo"]  = $"Thanh toan ve xe BookingId {booking.BookingId}",
                ["vnp_OrderType"]  = "other",
                ["vnp_Locale"]     = locale,
                ["vnp_ReturnUrl"]  = returnUrl,
                ["vnp_IpAddr"]     = ipAddr,
                ["vnp_CreateDate"] = createDate,
                ["vnp_ExpireDate"] = TimeZoneInfo.ConvertTimeFromUtc(booking.ExpiredAt, vnTimeZone)
                                                  .ToString("yyyyMMddHHmmss"),
            };

            // ── Tạo chuỗi raw hash (encode mềm, không encode key) ────────────
            var rawHash = BuildRawHashData(vnpParams);
            var secureHash = HmacSha512(hashSecret, rawHash);

            // ── Tạo query string đầy đủ cho URL ─────────────────────────────
            var queryString = BuildQueryString(vnpParams);
            return $"{baseUrl}?{queryString}&vnp_SecureHash={secureHash}";
        }

        // ─── Xác thực chữ ký VNPay trả về ──────────────────────────────────

        /// <summary>
        /// Kiểm tra vnp_SecureHash trong callback từ VNPay.
        /// Trả về true nếu chữ ký hợp lệ.
        /// </summary>
        public bool ValidateCallback(IQueryCollection query, out string responseCode)
        {
            var cfg = _config.GetSection("VnPay");
            var hashSecret = cfg["HashSecret"]!;

            responseCode = query["vnp_ResponseCode"].ToString();

            // Lấy hash từ VNPay gửi về
            var vnpSecureHash = query["vnp_SecureHash"].ToString();

            // Tái tạo sorted params — loại bỏ vnp_SecureHash và vnp_SecureHashType
            var vnpParams = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var (key, value) in query)
            {
                if (key.StartsWith("vnp_") &&
                    key != "vnp_SecureHash" &&
                    key != "vnp_SecureHashType" &&
                    !string.IsNullOrEmpty(value))
                {
                    vnpParams[key] = value!;
                }
            }

            var rawHash   = BuildRawHashData(vnpParams);
            var checkHash = HmacSha512(hashSecret, rawHash);

            return string.Equals(vnpSecureHash, checkHash, StringComparison.OrdinalIgnoreCase);
        }

        // ─── Helper: lấy TxnRef từ callback ────────────────────────────────

        /// <summary>
        /// Lấy BookingId từ vnp_TxnRef có dạng "{bookingId}-{timestamp}".
        /// </summary>
        public static int ExtractBookingId(string txnRef)
        {
            var parts = txnRef.Split('-');
            return int.TryParse(parts[0], out var id) ? id : 0;
        }

        // ─── Private helpers ─────────────────────────────────────────────────

        /// <summary>
        /// Tạo chuỗi raw data để ký: "key1=val1&amp;key2=val2" (đã sort, URL-encode value).
        /// </summary>
        private static string BuildRawHashData(SortedDictionary<string, string> parameters)
        {
            var sb = new StringBuilder();
            foreach (var (key, value) in parameters)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    if (sb.Length > 0) sb.Append('&');
                    sb.Append(key);
                    sb.Append('=');
                    sb.Append(WebUtility.UrlEncode(value));
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Tạo query string cho URL (giống BuildRawHashData nhưng dùng thêm khi gọi CreatePaymentUrl).
        /// </summary>
        private static string BuildQueryString(SortedDictionary<string, string> parameters)
            => BuildRawHashData(parameters);

        /// <summary>
        /// HMAC-SHA512 chuẩn VNPay — trả về chuỗi hex lowercase.
        /// </summary>
        private static string HmacSha512(string key, string data)
        {
            var keyBytes  = Encoding.UTF8.GetBytes(key);
            var dataBytes = Encoding.UTF8.GetBytes(data);
            using var hmac = new HMACSHA512(keyBytes);
            var hash = hmac.ComputeHash(dataBytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private string GetClientIpAddress()
        {
            var ctx = _httpContextAccessor.HttpContext;
            if (ctx == null) return "127.0.0.1";

            // Hỗ trợ reverse proxy / load balancer
            var forwarded = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwarded))
                return forwarded.Split(',')[0].Trim();

            return ctx.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        }
    }
}

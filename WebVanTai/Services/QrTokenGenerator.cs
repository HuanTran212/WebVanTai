using System.Security.Cryptography;
using System.Text;

namespace WebVanTai.Services
{
    /// <summary>
    /// Sinh QrToken bảo mật dùng chung cho mọi luồng xuất vé (VNPay, quầy vé tiền mặt).
    /// Token 64 ký tự hex: HMAC-SHA256(ticketId|tripId|nonce, secret) — nonce 128-bit ngẫu nhiên.
    /// </summary>
    public static class QrTokenGenerator
    {
        public static string Generate(int ticketId, int tripId, IConfiguration configuration)
        {
            var secret = configuration.GetSection("QrToken")["Secret"] ?? "WebVanTai-QrToken-Secret-2026";
            var nonce = RandomNumberGenerator.GetBytes(16);
            var payload = $"{ticketId}|{tripId}|{Convert.ToHexString(nonce)}";

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}

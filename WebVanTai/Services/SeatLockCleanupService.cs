using Microsoft.EntityFrameworkCore;
using WebVanTai.Data;
using WebVanTai.Models;
using WebVanTai.Models.Enums;

namespace WebVanTai.Services
{
    public class SeatLockCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<SeatLockCleanupService> _logger;

        public SeatLockCleanupService(
            IServiceProvider serviceProvider,
            ILogger<SeatLockCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SeatLockCleanupService đã khởi động, chu kỳ kiểm tra: mỗi 60 giây.");

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));

            try
            {
                while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
                {
                    await CleanupExpiredLocksAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("SeatLockCleanupService đang dừng lại do nhận yêu cầu hủy.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi ngoài ý muốn trong vòng lặp của SeatLockCleanupService.");
            }
        }

        public async Task CleanupExpiredLocksAsync(CancellationToken stoppingToken = default)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var now = DateTime.UtcNow;

                // 1. Quét danh sách đơn hàng quá hạn với TagWith (hỗ trợ profiler/query tracing)
                // và kiểm tra không khóa bảng để tránh làm nghẽn các request đặt vé của người dùng
                List<Booking> expiredBookings;
                try
                {
                    expiredBookings = await context.Bookings
                        .Include(b => b.Tickets)
                        .Where(b => b.Status == BookingStatus.Pending && b.ExpiredAt <= now)
                        .TagWith("SeatLockCleanupQuery")
                        .ToListAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "SeatLockCleanupService: Xung đột tạm thời khi quét đơn quá hạn. Bỏ qua chu kỳ này để nhường tài nguyên cho request của người dùng.");
                    return;
                }

                if (expiredBookings.Count == 0)
                {
                    return;
                }

                // 2. Bọc toàn bộ logic cập nhật trạng thái Expired và Void trong try-catch riêng biệt
                // Nếu có xung đột tạm thời với database (Deadlock/Lock Timeout), tự động bỏ qua chu kỳ này và thử lại sau 60s
                try
                {
                    var strategy = context.Database.CreateExecutionStrategy();
                    await strategy.ExecuteAsync(async () =>
                    {
                        using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted, stoppingToken);
                        try
                        {
                            int totalReleasedTickets = 0;

                            foreach (var booking in expiredBookings)
                            {
                                booking.Status = BookingStatus.Expired;

                                foreach (var ticket in booking.Tickets)
                                {
                                    if (ticket.Status != TicketStatus.Void)
                                    {
                                        ticket.Status = TicketStatus.Void;
                                        totalReleasedTickets++;
                                    }
                                }
                            }

                            await context.SaveChangesAsync(stoppingToken);
                            await transaction.CommitAsync(stoppingToken);

                            _logger.LogInformation(
                                "SeatLockCleanupService: Đã hủy {BookingCount} đơn giữ chỗ quá hạn và giải phóng {TicketCount} vé sang trạng thái Void.",
                                expiredBookings.Count,
                                totalReleasedTickets);
                        }
                        catch (Exception)
                        {
                            await transaction.RollbackAsync(stoppingToken);
                            throw;
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "SeatLockCleanupService: Xung đột tạm thời khi cập nhật hủy vé quá hạn. Bỏ qua chu kỳ này và sẽ tự động thử lại sau 60 giây.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong quá trình dọn dẹp các đơn đặt vé quá hạn.");
            }
        }
    }
}

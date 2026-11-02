using Microsoft.EntityFrameworkCore;
using WebVanTai.Data;
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

                // Tìm các booking Pending có thời gian hết hạn nhỏ hơn thời điểm hiện tại
                var expiredBookings = await context.Bookings
                    .Include(b => b.Tickets)
                    .Where(b => b.Status == BookingStatus.Pending && b.ExpiredAt <= now)
                    .ToListAsync(stoppingToken);

                if (expiredBookings.Count > 0)
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

                    _logger.LogInformation(
                        "SeatLockCleanupService: Đã hủy {BookingCount} đơn giữ chỗ quá hạn và giải phóng {TicketCount} vé sang trạng thái Void.",
                        expiredBookings.Count,
                        totalReleasedTickets);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong quá trình dọn dẹp các đơn đặt vé quá hạn.");
            }
        }
    }
}


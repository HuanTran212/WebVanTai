using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WebVanTai.Models;
using Route = WebVanTai.Models.Route;

namespace WebVanTai.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Station> Stations => Set<Station>();
        public DbSet<Route> Routes => Set<Route>();
        public DbSet<RouteStation> RouteStations => Set<RouteStation>();
        public DbSet<BusType> BusTypes => Set<BusType>();
        public DbSet<Bus> Buses => Set<Bus>();
        public DbSet<Trip> Trips => Set<Trip>();
        public DbSet<Booking> Bookings => Set<Booking>();
        public DbSet<Ticket> Tickets => Set<Ticket>();
        public DbSet<CrewMember> CrewMembers => Set<CrewMember>();
        public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Precision for Decimal fields
            builder.Entity<Station>(entity =>
            {
                entity.Property(e => e.Latitude).HasPrecision(9, 6);
                entity.Property(e => e.Longitude).HasPrecision(9, 6);
            });

            builder.Entity<Route>(entity =>
            {
                entity.Property(e => e.DurationHours).HasPrecision(4, 1);
            });

            builder.Entity<Trip>(entity =>
            {
                entity.Property(e => e.BasePrice).HasPrecision(18, 2);
            });

            // Bus unique license plate & crew assignments
            builder.Entity<Bus>(entity =>
            {
                entity.HasIndex(b => b.LicensePlate).IsUnique();

                entity.HasOne(b => b.Driver)
                    .WithMany(c => c.DrivenBuses)
                    .HasForeignKey(b => b.DriverId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(b => b.Assistant)
                    .WithMany(c => c.AssistedBuses)
                    .HasForeignKey(b => b.AssistantId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // RouteStation relations: Restrict on Station to prevent multiple cascade paths
            builder.Entity<RouteStation>(entity =>
            {
                entity.HasOne(rs => rs.Station)
                    .WithMany(s => s.RouteStations)
                    .HasForeignKey(rs => rs.StationId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(rs => rs.Route)
                    .WithMany(r => r.RouteStations)
                    .HasForeignKey(rs => rs.RouteId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Booking relations: Restrict on Stations and Users to prevent multiple cascade paths
            builder.Entity<Booking>(entity =>
            {
                entity.Property(e => e.TotalAmount).HasPrecision(18, 2);

                entity.HasOne(b => b.PickupStation)
                    .WithMany(s => s.PickupBookings)
                    .HasForeignKey(b => b.PickupStationId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(b => b.DropoffStation)
                    .WithMany(s => s.DropoffBookings)
                    .HasForeignKey(b => b.DropoffStationId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(b => b.User)
                    .WithMany(u => u.Bookings)
                    .HasForeignKey(b => b.UserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Nhân viên Quản lý vé xuất đơn (nếu bán tại quầy) — không có navigation ngược trên ApplicationUser
                entity.HasOne(b => b.SoldByUser)
                    .WithMany()
                    .HasForeignKey(b => b.SoldByUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(b => b.Trip)
                    .WithMany(t => t.Bookings)
                    .HasForeignKey(b => b.TripId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Ticket relations: Restrict on Trip (already cascades via Booking)
            // Concurrency & Double-Booking Protection: Unique Filtered Index when Status != Void (2)
            builder.Entity<Ticket>(entity =>
            {
                entity.HasOne(t => t.Trip)
                    .WithMany(tr => tr.Tickets)
                    .HasForeignKey(t => t.TripId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(t => t.Booking)
                    .WithMany(b => b.Tickets)
                    .HasForeignKey(t => t.BookingId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(t => new { t.TripId, t.SeatCode })
                    .IsUnique()
                    .HasFilter("[Status] <> 2");
            });
        }
    }
}

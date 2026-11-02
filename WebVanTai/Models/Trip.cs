using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WebVanTai.Models.Enums;

namespace WebVanTai.Models
{
    public class Trip
    {
        [Key]
        public int TripId { get; set; }

        public int RouteId { get; set; }

        public int BusId { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime DepartureTime { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime ArrivalTime { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BasePrice { get; set; }

        public TripStatus Status { get; set; } = TripStatus.Scheduled;

        [ForeignKey(nameof(RouteId))]
        public virtual Route? Route { get; set; }

        [ForeignKey(nameof(BusId))]
        public virtual Bus? Bus { get; set; }

        public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
        public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    }
}


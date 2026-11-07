using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WebVanTai.Models.Enums;

namespace WebVanTai.Models
{
    public class Ticket
    {
        [Key]
        public int TicketId { get; set; }

        public int BookingId { get; set; }

        public int TripId { get; set; }

        [Required]
        [StringLength(10)]
        [Column(TypeName = "nvarchar(10)")]
        public string SeatCode { get; set; } = string.Empty;

        [StringLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        public string PassengerName { get; set; } = string.Empty;

        [StringLength(20)]
        [Column(TypeName = "nvarchar(20)")]
        public string PassengerPhone { get; set; } = string.Empty;

        [StringLength(256)]
        [Column(TypeName = "nvarchar(256)")]
        public string? QrToken { get; set; }

        public TicketStatus Status { get; set; } = TicketStatus.Valid;

        [Column(TypeName = "datetime2")]
        public DateTime? CheckedInAt { get; set; }

        [ForeignKey(nameof(BookingId))]
        public virtual Booking? Booking { get; set; }

        [ForeignKey(nameof(TripId))]
        public virtual Trip? Trip { get; set; }
    }
}


using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WebVanTai.Models.Enums;

namespace WebVanTai.Models
{
    public class Booking
    {
        [Key]
        public int BookingId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public int TripId { get; set; }

        public int PickupStationId { get; set; }

        public int DropoffStationId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        public BookingStatus Status { get; set; } = BookingStatus.Pending;

        [Column(TypeName = "datetime2")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Column(TypeName = "datetime2")]
        public DateTime ExpiredAt { get; set; }

        [StringLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        public string? VnPayTxnRef { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser? User { get; set; }

        [ForeignKey(nameof(TripId))]
        public virtual Trip? Trip { get; set; }

        [ForeignKey(nameof(PickupStationId))]
        public virtual Station? PickupStation { get; set; }

        [ForeignKey(nameof(DropoffStationId))]
        public virtual Station? DropoffStation { get; set; }

        public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    }
}


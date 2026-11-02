using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebVanTai.Models
{
    public class Station
    {
        [Key]
        public int StationId { get; set; }

        [Required]
        [StringLength(150)]
        [Column(TypeName = "nvarchar(150)")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        public string Province { get; set; } = string.Empty;

        [Required]
        [StringLength(250)]
        [Column(TypeName = "nvarchar(250)")]
        public string Address { get; set; } = string.Empty;

        [Column(TypeName = "decimal(9,6)")]
        public decimal Latitude { get; set; }

        [Column(TypeName = "decimal(9,6)")]
        public decimal Longitude { get; set; }

        public virtual ICollection<RouteStation> RouteStations { get; set; } = new List<RouteStation>();
        public virtual ICollection<Booking> PickupBookings { get; set; } = new List<Booking>();
        public virtual ICollection<Booking> DropoffBookings { get; set; } = new List<Booking>();
    }
}


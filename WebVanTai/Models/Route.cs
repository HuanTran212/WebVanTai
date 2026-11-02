using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebVanTai.Models
{
    public class Route
    {
        [Key]
        public int RouteId { get; set; }

        [Required]
        [StringLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        public string StartProvince { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        public string EndProvince { get; set; } = string.Empty;

        public int DistanceKm { get; set; }

        [Column(TypeName = "decimal(4,1)")]
        public decimal DurationHours { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual ICollection<RouteStation> RouteStations { get; set; } = new List<RouteStation>();
        public virtual ICollection<Trip> Trips { get; set; } = new List<Trip>();
    }
}


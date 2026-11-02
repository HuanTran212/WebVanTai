using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebVanTai.Models
{
    public class Bus
    {
        [Key]
        public int BusId { get; set; }

        public int BusTypeId { get; set; }

        [Required]
        [StringLength(20)]
        [Column(TypeName = "nvarchar(20)")]
        public string LicensePlate { get; set; } = string.Empty;

        [StringLength(250)]
        [Column(TypeName = "nvarchar(250)")]
        public string Amenities { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        [ForeignKey(nameof(BusTypeId))]
        public virtual BusType? BusType { get; set; }

        public virtual ICollection<Trip> Trips { get; set; } = new List<Trip>();
    }
}


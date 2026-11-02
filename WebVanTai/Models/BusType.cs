using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebVanTai.Models
{
    public class BusType
    {
        [Key]
        public int BusTypeId { get; set; }

        [Required]
        [StringLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        public string Name { get; set; } = string.Empty;

        public int TotalSeats { get; set; }

        public int TotalDecks { get; set; }

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string SeatLayoutJson { get; set; } = string.Empty;

        public virtual ICollection<Bus> Buses { get; set; } = new List<Bus>();
    }
}


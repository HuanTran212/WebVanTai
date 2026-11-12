using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WebVanTai.Models.Enums;

namespace WebVanTai.Models
{
    public class CrewMember
    {
        [Key]
        public int CrewMemberId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
        [StringLength(100)]
        [Column(TypeName = "nvarchar(100)")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [StringLength(20)]
        [Column(TypeName = "nvarchar(20)")]
        public string PhoneNumber { get; set; } = string.Empty;

        [StringLength(50)]
        [Column(TypeName = "nvarchar(50)")]
        public string? LicenseNumber { get; set; }

        public CrewRole Role { get; set; } = CrewRole.Driver;

        public bool IsActive { get; set; } = true;

        [Column(TypeName = "datetime2")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<Bus> DrivenBuses { get; set; } = new List<Bus>();
        public virtual ICollection<Bus> AssistedBuses { get; set; } = new List<Bus>();
    }
}

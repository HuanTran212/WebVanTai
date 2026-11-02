using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using WebVanTai.Models.Enums;

namespace WebVanTai.Models
{
    public class RouteStation
    {
        [Key]
        public int Id { get; set; }

        public int RouteId { get; set; }

        public int StationId { get; set; }

        public int StopOrder { get; set; }

        public StationType StationType { get; set; }

        public int OffsetMinutes { get; set; }

        [ForeignKey(nameof(RouteId))]
        public virtual Route? Route { get; set; }

        [ForeignKey(nameof(StationId))]
        public virtual Station? Station { get; set; }
    }
}


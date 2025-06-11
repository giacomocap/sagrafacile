using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SagraFacile.NET.API.Models
{
    public class AreaDayOrderSequence
    {
        [Key]
        public int Id { get; set; }

        public int AreaId { get; set; }
        [ForeignKey("AreaId")]
        public virtual Area Area { get; set; } = null!;

        public int DayId { get; set; }
        [ForeignKey("DayId")]
        public virtual Day Day { get; set; } = null!;

        public int LastSequenceNumber { get; set; }
    }
}

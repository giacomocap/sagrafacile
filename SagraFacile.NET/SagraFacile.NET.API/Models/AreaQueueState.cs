using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SagraFacile.NET.API.Models
{
    public class AreaQueueState
    {
        [Key]
        public int AreaQueueStateId { get; set; }

        // Foreign Key to Area
        // This creates a one-to-one relationship with Area
        // An Area can have at most one AreaQueueState
        public int AreaId { get; set; }

        [ForeignKey("AreaId")]
        public virtual Area? Area { get; set; }

        [Required]
        public int NextSequentialNumber { get; set; } = 1;

        public int? LastCalledNumber { get; set; }

        // Foreign Key to CashierStation
        public int? LastCalledCashierStationId { get; set; } // Assuming CashierStationId is int

        [ForeignKey("LastCalledCashierStationId")]
        public virtual CashierStation? LastCalledCashierStation { get; set; } // Navigation property

        public DateTime? LastCallTimestamp { get; set; }

        public DateTime? LastResetTimestamp { get; set; }
    }
} 
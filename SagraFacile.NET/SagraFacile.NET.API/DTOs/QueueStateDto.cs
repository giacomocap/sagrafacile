using System;

namespace SagraFacile.NET.API.DTOs
{
    public class QueueStateDto
    {
        public int AreaId { get; set; }
        public int NextSequentialNumber { get; set; }
        public int? LastCalledNumber { get; set; }
        public int? LastCalledCashierStationId { get; set; }
        public string? LastCalledCashierStationName { get; set; }
        public DateTime? LastCallTimestamp { get; set; }
        public DateTime? LastResetTimestamp { get; set; }
        public bool IsQueueSystemEnabled { get; set; }
    }
} 
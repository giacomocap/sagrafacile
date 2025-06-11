using System;

namespace SagraFacile.NET.API.DTOs
{
    public class CalledNumberBroadcastDto
    {
        public int AreaId { get; set; }
        public int TicketNumber { get; set; }
        public int CashierStationId { get; set; }
        public string CashierStationName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
} 
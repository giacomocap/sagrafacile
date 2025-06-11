using System;

namespace SagraFacile.NET.API.DTOs
{
    public class StockUpdateBroadcastDto
    {
        public int MenuItemId { get; set; }
        public int AreaId { get; set; } // To help frontend target updates
        public int? NewScorta { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}

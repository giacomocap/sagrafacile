using System;

namespace SagraFacile.NET.API.DTOs.Analytics
{
    public class AverageOrderValueTrendDto
    {
        public string Date { get; set; } // Consider using DateTime and formatting on client
        public decimal AverageValue { get; set; }
        public int OrderCount { get; set; }
        public int? DayId { get; set; } // Changed to nullable int
    }
}

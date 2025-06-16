using System;

namespace SagraFacile.NET.API.DTOs.Analytics
{
    public class DashboardKPIsDto
    {
        public decimal TodayTotalSales { get; set; }
        public int TodayOrderCount { get; set; }
        public decimal AverageOrderValue { get; set; }
        public string MostPopularCategory { get; set; }
        public int DayId { get; set; }
        public string DayDate { get; set; } // Consider using DateTime and formatting on client
    }
}

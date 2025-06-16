namespace SagraFacile.NET.API.DTOs.Analytics
{
    public class OrdersByHourDto
    {
        public int Hour { get; set; } // 0-23
        public int OrderCount { get; set; }
        public decimal Revenue { get; set; }
    }
}

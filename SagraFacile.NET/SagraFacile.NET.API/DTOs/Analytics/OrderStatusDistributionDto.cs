namespace SagraFacile.NET.API.DTOs.Analytics
{
    public class OrderStatusDistributionDto
    {
        public string Status { get; set; }
        public int Count { get; set; }
        public decimal Percentage { get; set; }
    }
}

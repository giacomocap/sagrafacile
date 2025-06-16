namespace SagraFacile.NET.API.DTOs.Analytics
{
    public class PaymentMethodDistributionDto
    {
        public string PaymentMethod { get; set; } // e.g., "Cash", "POS"
        public int Count { get; set; }
        public decimal Amount { get; set; }
        public decimal Percentage { get; set; } // Percentage of total transactions or total amount
    }
}

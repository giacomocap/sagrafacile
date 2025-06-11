namespace SagraFacile.NET.API.DTOs
{
    public class CalledNumberDto
    {
        public int TicketNumber { get; set; }
        public int CashierStationId { get; set; }
        public string CashierStationName { get; set; } = string.Empty;
    }
} 
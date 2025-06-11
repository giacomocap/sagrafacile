namespace SagraFacile.NET.API.DTOs
{
    public class CashierStationDto
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public int AreaId { get; set; }
        public string AreaName { get; set; } // For display purposes
        public string Name { get; set; }
        public int ReceiptPrinterId { get; set; }
        public string ReceiptPrinterName { get; set; } // For display purposes
        public bool PrintComandasAtThisStation { get; set; }
        public bool IsEnabled { get; set; }
    }
} 
namespace SagraFacile.NET.API.DTOs;

/// <summary>
/// Data Transfer Object for Area information.
/// Used for returning Area details.
/// </summary>
public class AreaDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }

    // Workflow Flags
    public bool EnableWaiterConfirmation { get; set; }
    public bool EnableKds { get; set; }
    public bool EnableCompletionConfirmation { get; set; }
    public bool EnableQueueSystem { get; set; }

    // Printing fields
    public int? ReceiptPrinterId { get; set; }
    public bool PrintComandasAtCashier { get; set; }

    // New Charges
    public decimal GuestCharge { get; set; }
    public decimal TakeawayCharge { get; set; }

    // Consider adding OrganizationName if needed for display purposes
    // public string OrganizationName { get; set; } = string.Empty;
}

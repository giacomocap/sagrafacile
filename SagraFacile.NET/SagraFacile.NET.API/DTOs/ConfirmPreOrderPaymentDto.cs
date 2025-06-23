using System.ComponentModel.DataAnnotations;
using SagraFacile.NET.API.DTOs;

public class ConfirmPreOrderPaymentDto
{
    [Required]
    [MinLength(1, ErrorMessage = "Order must contain at least one item.")]
    public required List<CreateOrderItemDto> Items { get; set; }

    [Required(ErrorMessage = "Payment method is required.")]
    public string PaymentMethod { get; set; } = string.Empty;

    [Range(0, (double)decimal.MaxValue, ErrorMessage = "Amount paid cannot be negative.")]
    public decimal? AmountPaid { get; set; }

    [StringLength(100, ErrorMessage = "Customer name cannot exceed 100 characters.")]
    public string? CustomerName { get; set; } // Made nullable to match frontend flexibility, service validates final state

    [Range(1, int.MaxValue, ErrorMessage = "Number of guests must be at least 1 unless it's takeaway.")] // Adjusted Range for NumberOfGuests
    public int NumberOfGuests { get; set; }

    public bool IsTakeaway { get; set; }

    public int? CashierStationId { get; set; } // Added CashierStationId
}
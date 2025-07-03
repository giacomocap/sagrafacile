using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SagraFacile.NET.API.Models
{
    public enum OrderStatus
    {
        PreOrder,       // Order placed via public interface, not yet confirmed/paid
        Pending,        // Order created by cashier, not yet paid/processed
        Paid,           // Order paid
        Preparing,      // Order confirmed by waiter, sent to kitchen/bar for preparation
        ReadyForPickup, // Order preparation completed by KDS, ready for pickup
        Completed,      // Order picked up/served
        Cancelled       // Order cancelled
    }

    public class Order
    {
        [Key] // Explicitly adding Key attribute
        [StringLength(50)] // Max length for combined ID (adjust if needed)
        public string Id { get; set; } = null!;

        // Platform ID from the SagraPreOrdine companion app, if applicable
        [StringLength(100)] // Assuming platform IDs are strings, adjust length as needed
        public string? PreOrderPlatformId { get; set; }

        /// <summary>
        /// Human-readable order number for display purposes (e.g., CUC-001).
        /// Generated based on Area.Slug and a daily sequence.
        /// </summary>
        [StringLength(50)] // Max length for display order number (e.g., "SLG-9999")
        public string? DisplayOrderNumber { get; set; } // Nullable as existing orders won't have it initially

        // Removed OrderNumber property

        // Foreign Key for Organization
        public Guid OrganizationId { get; set; }

        // Navigation property for Organization
        [ForeignKey("OrganizationId")]
        public virtual Organization? Organization { get; set; }

        // Foreign Key for Area
        public int AreaId { get; set; }

        // Navigation property for Area
        [ForeignKey("AreaId")]
        public virtual Area? Area { get; set; }

        // Foreign Key for Cashier (User) - Nullable for Pre-orders
        public string? CashierId { get; set; } // Using string because IdentityUser uses string IDs

        // Navigation property for Cashier
        [ForeignKey("CashierId")]
        public virtual User? Cashier { get; set; }

        // Foreign Key for Waiter (User) - Nullable, set upon confirmation
        public string? WaiterId { get; set; } // ID of the user who confirmed preparation (Waiter)

        // Navigation property for Waiter
        [ForeignKey("WaiterId")]
        public virtual User? Waiter { get; set; }

        // Foreign Key for Day (Operational Day) - Nullable initially, set on confirmation/creation
        public int? DayId { get; set; }

        // Navigation property for Day
        [ForeignKey("DayId")]
        public virtual Day? Day { get; set; }

        // Customer details for Pre-orders
        [StringLength(100)]
        public string? CustomerName { get; set; } // Required for PreOrder, optional otherwise

        [StringLength(255)]
        [EmailAddress]
        public string? CustomerEmail { get; set; } // Required for PreOrder, optional otherwise

        // Table number assigned by the waiter
        [StringLength(50)] // Adjust length as needed
        public string? TableNumber { get; set; }

        /// <summary>
        /// Number of guests for this order. ("Coperti")
        /// </summary>
        public int NumberOfGuests { get; set; } = 1; // Default to 1 guests

        /// <summary>
        /// Indicates if the order is for takeaway. ("Asporto")
        /// </summary>
        public bool IsTakeaway { get; set; } = false; // Default to not takeaway

        [Required]
        public OrderStatus Status { get; set; } = OrderStatus.Pending; // Default for cashier orders

        [Required]
        public DateTime OrderDateTime { get; set; } = DateTime.UtcNow; // Use UTC for consistency

        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal TotalAmount { get; set; }

        [StringLength(50)]
        public string? PaymentMethod { get; set; } // e.g., "Contanti", "POS"

        // Optional: Amount paid if cash, for calculating change
        [Column(TypeName = "decimal(18, 2)")]
        public decimal? AmountPaid { get; set; }

        // Navigation property for OrderItems
        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

        public int? CashierStationId { get; set; }
        [ForeignKey("CashierStationId")]
        public CashierStation? CashierStation { get; set; }
    }
}

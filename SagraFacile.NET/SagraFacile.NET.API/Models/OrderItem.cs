using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SagraFacile.NET.API.Models
{
    public enum KdsStatus
    {
        Pending = 0,
        Confirmed = 1
    }

    public class OrderItem
    {
        public int Id { get; set; }

        // Foreign Key for Order
        [StringLength(50)] // Match Order.Id length
        public string OrderId { get; set; } = null!; // Changed to string

        // Navigation property for Order
        [ForeignKey("OrderId")]
        public virtual Order? Order { get; set; }

        // Foreign Key for MenuItem
        public int MenuItemId { get; set; }

        // Navigation property for MenuItem
        [ForeignKey("MenuItemId")]
        public virtual MenuItem? MenuItem { get; set; }

        [Required]
        public int Quantity { get; set; }

        // Store the price per unit at the time the order was placed
        // This prevents issues if the MenuItem price changes later
        [Required]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal UnitPrice { get; set; }

        // Optional note for the specific item in the order (e.g., cooking instructions)
        [StringLength(255)]
        public string? Note { get; set; }

        // Status for the Kitchen Display System (KDS)
        [Required]
        public KdsStatus KdsStatus { get; set; } = KdsStatus.Pending;
    }
}

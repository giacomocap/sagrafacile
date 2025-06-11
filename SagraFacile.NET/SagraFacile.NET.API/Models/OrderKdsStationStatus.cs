using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SagraFacile.NET.API.Models
{
    /// <summary>
    /// Tracks the confirmation status of an order for a specific KDS station.
    /// An entry is created implicitly when needed, and IsConfirmed is set to true
    /// when the station operator confirms completion of their assigned items for the order.
    /// </summary>
    public class OrderKdsStationStatus
    {
        // Composite Primary Key defined in DbContext

        [Required]
        public string OrderId { get; set; } = null!; // Foreign Key to Order

        [Required]
        public int KdsStationId { get; set; } // Foreign Key to KdsStation

        [Required]
        public bool IsConfirmed { get; set; } = false; // Default to false

        // Navigation properties
        [ForeignKey("OrderId")]
        public virtual Order? Order { get; set; }

        [ForeignKey("KdsStationId")]
        public virtual KdsStation? KdsStation { get; set; }
    }
}

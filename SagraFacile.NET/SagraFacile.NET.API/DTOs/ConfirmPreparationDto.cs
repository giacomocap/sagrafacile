using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.DTOs
{
    /// <summary>
    /// DTO for confirming an order's preparation and assigning a table number.
    /// Used by the Waiter interface.
    /// </summary>
    public class ConfirmPreparationDto
    {
        [Required(ErrorMessage = "Table number is required.")]
        [StringLength(50, ErrorMessage = "Table number cannot exceed 50 characters.")]
        public required string TableNumber { get; set; }
    }
}

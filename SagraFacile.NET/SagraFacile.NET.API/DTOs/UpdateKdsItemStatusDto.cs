using SagraFacile.NET.API.Models; // For KdsStatus enum
using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.DTOs
{
    public class UpdateKdsItemStatusDto
    {
        [Required]
        [EnumDataType(typeof(KdsStatus), ErrorMessage = "Invalid KDS Status value.")]
        public KdsStatus KdsStatus { get; set; }
    }
}

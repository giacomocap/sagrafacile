using SagraFacile.NET.API.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.DTOs
{
    public class ReprintRequestDto
    {
        [Required]
        public ReprintType ReprintJobType { get; set; }

        public int? PrinterId { get; set; } // Optional: For admin-specified printer
    }
} 
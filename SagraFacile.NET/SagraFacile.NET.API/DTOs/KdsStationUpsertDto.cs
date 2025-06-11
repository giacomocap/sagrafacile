using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.DTOs
{
    public class KdsStationUpsertDto
    {
        [Required(ErrorMessage = "KDS Station name is required.")]
        [MaxLength(100, ErrorMessage = "KDS Station name cannot exceed 100 characters.")]
        public string Name { get; set; } = null!;
    }
}

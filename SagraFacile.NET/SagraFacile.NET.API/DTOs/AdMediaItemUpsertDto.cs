using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.DTOs
{
    public class AdMediaItemUpsertDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        public IFormFile File { get; set; }
    }
}

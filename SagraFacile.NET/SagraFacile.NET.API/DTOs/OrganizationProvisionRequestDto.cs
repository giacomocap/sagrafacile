using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.DTOs
{
    public class OrganizationProvisionRequestDto
    {
        [Required]
        [StringLength(100, MinimumLength = 3)]
        public string OrganizationName { get; set; }
    }
}

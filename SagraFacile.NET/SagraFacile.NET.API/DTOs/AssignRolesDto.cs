using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.DTOs
{
    public class AssignRolesDto
    {
        [Required]
        public required string UserId { get; set; }

        [Required]
        public required List<string> RoleNames { get; set; }
    }
}

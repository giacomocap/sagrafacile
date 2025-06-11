using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.DTOs;

public class RefreshTokenRequestDto
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

namespace SagraFacile.NET.API.DTOs;

/// <summary>
/// Data Transfer Object for Menu Category information.
/// Used for returning Menu Category details.
/// </summary>
public class MenuCategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int AreaId { get; set; }
    // Consider adding AreaName if needed for display purposes
    // public string AreaName { get; set; } = string.Empty;
}

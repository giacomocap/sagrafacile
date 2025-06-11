using SagraFacile.NET.API.Models;

namespace SagraFacile.NET.API.DTOs;

public class MenuItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int MenuCategoryId { get; set; }
    // Removed MenuCategory navigation property to break cycle
    // public MenuCategoryDto? MenuCategory { get; set; } // Consider adding if needed, ensure MenuCategoryDto exists and also avoids cycles
    public bool IsNoteRequired { get; set; }
    public string? NoteSuggestion { get; set; }
    public int? Scorta { get; set; }

    // Optional: Add a constructor or mapping method if needed
    public MenuItemDto() { }

    public MenuItemDto(MenuItem item)
    {
        Id = item.Id;
        Name = item.Name;
        Description = item.Description;
        Price = item.Price;
        MenuCategoryId = item.MenuCategoryId;
        IsNoteRequired = item.IsNoteRequired;
        NoteSuggestion = item.NoteSuggestion;
        Scorta = item.Scorta;
        // If MenuCategoryDto is added later, map it here:
        // MenuCategory = item.MenuCategory == null ? null : new MenuCategoryDto(item.MenuCategory);
    }
}

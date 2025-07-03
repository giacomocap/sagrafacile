using System.ComponentModel;

namespace SagraFacile.NET.API.DTOs;

public class OrderQueryParameters
{
    private const int MaxPageSize = 100;
    private int? _pageSize;

    public int? Page { get; set; }

    public int? PageSize
    {
        get => _pageSize;
        set => _pageSize = (value.HasValue && value.Value > MaxPageSize) ? MaxPageSize : value;
    }

    [DefaultValue("orderDateTime")]
    public string SortBy { get; set; } = "orderDateTime";

    [DefaultValue(false)]
    public bool SortAscending { get; set; } = false;

    // Filtering properties
    public int? AreaId { get; set; }
    public int? DayId { get; set; }
    public Guid? OrganizationId { get; set; }
    public List<int>? Statuses { get; set; }
}

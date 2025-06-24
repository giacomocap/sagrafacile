namespace SagraFacile.NET.API.DTOs
{
    public class PrintJobQueryParameters
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? SortBy { get; set; }
        public bool SortAscending { get; set; } = false;
        // Add filters later if needed
    }
}

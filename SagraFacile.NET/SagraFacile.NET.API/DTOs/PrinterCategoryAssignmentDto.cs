namespace SagraFacile.NET.API.DTOs
{
    public class PrinterCategoryAssignmentDto
    {
        public int PrinterId { get; set; }
        public int MenuCategoryId { get; set; }
        public string MenuCategoryName { get; set; } = null!;
        public int MenuCategoryAreaId { get; set; }
    }
}

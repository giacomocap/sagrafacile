using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SagraFacile.NET.API.Models
{
    public enum DayStatus
    {
        Open,
        Closed
    }

    public class Day
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid OrganizationId { get; set; }
        [ForeignKey("OrganizationId")]
        public Organization Organization { get; set; } = null!;

        [Required]
        public DateTime StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        [Required]
        public DayStatus Status { get; set; }

        [Required]
        public string OpenedByUserId { get; set; } = null!;
        [ForeignKey("OpenedByUserId")]
        public User OpenedByUser { get; set; } = null!;

        public string? ClosedByUserId { get; set; }
        [ForeignKey("ClosedByUserId")]
        public User? ClosedByUser { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? TotalSales { get; set; } // Potentially calculated on close

        // Navigation property for Orders associated with this Day
        public ICollection<Order> Orders { get; set; } = new List<Order>();
    }
}

using System;
using SagraFacile.NET.API.Models; // Assuming DayStatus is in Models

namespace SagraFacile.NET.API.DTOs
{
    public class DayDto
    {
        public int Id { get; set; }
        public Guid OrganizationId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public DayStatus Status { get; set; }
        public string OpenedByUserId { get; set; } = null!;
        public string? OpenedByUserFirstName { get; set; } // Optional: Include user details
        public string? OpenedByUserLastName { get; set; } // Optional: Include user details
        public string? ClosedByUserId { get; set; }
        public string? ClosedByUserFirstName { get; set; } // Optional: Include user details
        public string? ClosedByUserLastName { get; set; } // Optional: Include user details
        public decimal? TotalSales { get; set; }
    }
}

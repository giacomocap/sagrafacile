using SagraFacile.NET.API.Models.Enums;
using System;

namespace SagraFacile.NET.API.DTOs
{
    public class PrintJobDto
    {
        public Guid Id { get; set; }
        public PrintJobType JobType { get; set; }
        public PrintJobStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastAttemptAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int RetryCount { get; set; }
        public string? ErrorMessage { get; set; }
        public string? OrderId { get; set; }
        public string? OrderDisplayNumber { get; set; }
        public int PrinterId { get; set; }
        public string PrinterName { get; set; } = string.Empty;
    }
}

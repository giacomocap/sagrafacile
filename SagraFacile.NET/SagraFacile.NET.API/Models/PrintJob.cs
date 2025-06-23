using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SagraFacile.NET.API.Models.Enums;

namespace SagraFacile.NET.API.Models
{
    public class PrintJob
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public int OrganizationId { get; set; }
        public Organization Organization { get; set; }

        public int? AreaId { get; set; }
        public Area? Area { get; set; }

        public string? OrderId { get; set; }
        public Order? Order { get; set; }

        [Required]
        public int PrinterId { get; set; }
        public Printer Printer { get; set; }

        [Required]
        public PrintJobType JobType { get; set; }

        [Required]
        public PrintJobStatus Status { get; set; }

        [Required]
        public byte[] Content { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastAttemptAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public int RetryCount { get; set; } = 0;

        public string? ErrorMessage { get; set; }
    }
}

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SagraFacile.NET.API.DTOs
{
    /// <summary>
    /// DTO for setting the complete list of menu category assignments for a printer.
    /// </summary>
    public class SetPrinterAssignmentsDto
    {
        /// <summary>
        /// A list of MenuCategory IDs to be assigned to the printer.
        /// Any existing assignments for this printer not included in this list will be removed.
        /// An empty list will remove all assignments.
        /// </summary>
        [Required]
        public IEnumerable<int> CategoryIds { get; set; } = new List<int>();
    }
} 
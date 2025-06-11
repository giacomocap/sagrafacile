using SagraFacile.NET.API.DTOs; // Added for DTO
using SagraFacile.NET.API.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services.Interfaces
{
    public interface IPrinterAssignmentService
    {
        Task<IEnumerable<PrinterCategoryAssignmentDto>> GetAssignmentsForPrinterAsync(int printerId, int areaId); // Changed return type
        Task<(bool Success, string? Error)> SetAssignmentsForPrinterAsync(int printerId, int areaId, IEnumerable<int> menuCategoryIds);
    }
}

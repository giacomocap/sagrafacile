using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services.Interfaces
{
    public interface IAdAreaAssignmentService
    {
        Task<IEnumerable<AdAreaAssignmentDto>> GetAssignmentsForAreaAsync(int areaId);
        Task<ServiceResult<AdAreaAssignmentDto>> CreateAssignmentAsync(AdAreaAssignmentUpsertDto assignmentDto);
        Task<ServiceResult> UpdateAssignmentAsync(Guid assignmentId, AdAreaAssignmentUpsertDto assignmentDto);
        Task<ServiceResult> DeleteAssignmentAsync(Guid assignmentId);
    }
}

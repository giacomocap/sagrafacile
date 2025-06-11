using SagraFacile.NET.API.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services.Interfaces
{
    public interface IAdMediaItemService
    {
        Task<IEnumerable<AdMediaItemDto>> GetAdsByOrganizationAsync(int organizationId);
        Task<(AdMediaItemDto? createdAd, string? error)> CreateAdAsync(int organizationId, AdMediaItemUpsertDto adDto);
        Task<(bool success, string? error)> UpdateAdAsync(Guid adId, AdMediaItemUpsertDto adDto);
        Task<(bool success, string? error)> DeleteAdAsync(Guid adId);
        Task<IEnumerable<AdMediaItemDto>> GetActiveAdsByAreaAsync(int areaId);
    }
}

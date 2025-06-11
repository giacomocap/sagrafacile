using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Models.Results;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services.Interfaces
{
    public interface IQueueService
    {
        Task<ServiceResult<CalledNumberDto>> CallNextAsync(int areaId, int cashierStationId);
        Task<ServiceResult<CalledNumberDto>> CallSpecificAsync(int areaId, int cashierStationId, int ticketNumber);
        Task<ServiceResult<QueueStateDto>> GetQueueStateAsync(int areaId);
        Task<ServiceResult> ResetQueueAsync(int areaId, int startingNumber = 1);
        Task<ServiceResult> UpdateNextSequentialNumberAsync(int areaId, int newNextNumber);
        Task<ServiceResult> ToggleQueueSystemAsync(int areaId, bool enable);

        // New method for public consumption
        Task<ServiceResult<List<CashierStationDto>>> GetActiveCashierStationsForAreaAsync(int areaId);
        Task<ServiceResult<CalledNumberDto>> RespeakLastCalledNumberAsync(int areaId, int cashierStationId);
    }
}

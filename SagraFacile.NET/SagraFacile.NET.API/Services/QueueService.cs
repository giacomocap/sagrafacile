using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SagraFacile.NET.API.Data;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Hubs; // Assuming OrderHub is used
using SagraFacile.NET.API.Models;
using SagraFacile.NET.API.Models.Results;
using SagraFacile.NET.API.Services.Interfaces;
using System;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Services
{
    public class QueueService : BaseService, IQueueService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHubContext<OrderHub> _hubContext;
        private readonly ILogger<QueueService> _logger;

        public QueueService(
            ApplicationDbContext context,
            IHubContext<OrderHub> hubContext,
            ILogger<QueueService> logger,
            IHttpContextAccessor httpContextAccessor)
            : base(httpContextAccessor)
        {
            _context = context;
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task<ServiceResult<CalledNumberDto>> CallNextAsync(int areaId, int cashierStationId)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            if (!isSuperAdmin && !userOrgId.HasValue)
            {
                return ServiceResult<CalledNumberDto>.Fail("User organization context could not be determined.");
            }

            // Verify CashierStation exists, is enabled, and belongs to the correct Area and Organization
            var cashierStation = await _context.CashierStations
                                                .AsNoTracking()
                                                .FirstOrDefaultAsync(cs => cs.Id == cashierStationId && cs.IsEnabled);

            if (cashierStation == null)
            {
                return ServiceResult<CalledNumberDto>.Fail($"Cashier station with ID {cashierStationId} not found or is disabled.");
            }
            if (cashierStation.AreaId != areaId)
            {
                 _logger.LogWarning("Cashier station {CashierStationId} in Area {StationAreaId} used to call next for Area {AreaId}", cashierStationId, cashierStation.AreaId, areaId);
                 return ServiceResult<CalledNumberDto>.Fail("Cashier station does not belong to the specified area.");
            }
            if (!isSuperAdmin && cashierStation.OrganizationId != userOrgId)
            {
                _logger.LogWarning("User from Org {UserOrgId} attempted to use CashierStation {CashierStationId} in Org {StationOrgId}", userOrgId, cashierStationId, cashierStation.OrganizationId);
                return ServiceResult<CalledNumberDto>.Fail($"Cashier station with ID {cashierStationId} not found."); // Mask as not found
            }

            // Use the helper to get/create queue state; this also checks area existence/access
            var queueState = await GetOrCreateQueueStateAsync(areaId);
            if (queueState == null)
            {
                 return ServiceResult<CalledNumberDto>.Fail($"Could not access queue state for Area ID {areaId}.");
            }

            // Ensure queue system is enabled for the area (check Area directly)
             var area = await _context.Areas.AsNoTracking().FirstOrDefaultAsync(a => a.Id == areaId);
             if (area == null || !area.EnableQueueSystem)
            {
                 _logger.LogWarning("Attempted to call next for AreaId {AreaId} where queue system is disabled.", areaId);
                 return ServiceResult<CalledNumberDto>.Fail("Queue system is not enabled for this area.");
            }

            // --- Critical Section: Get next number and update state --- 
            // For simplicity, we'll use optimistic concurrency control provided by EF Core.
            // A more robust solution might involve row-level locking (e.g., FOR UPDATE) 
            // if high contention is expected, but that adds complexity.

            int numberToCall = queueState.NextSequentialNumber;
            queueState.NextSequentialNumber++; // Increment for the next call
            queueState.LastCalledNumber = numberToCall;
            queueState.LastCalledCashierStationId = cashierStationId;
            queueState.LastCallTimestamp = DateTime.UtcNow;

            _context.AreaQueueStates.Update(queueState);

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("User {UserId} at Station {StationId} ({StationName}) called next number {NumberToCall} for Area {AreaId}", GetUserId(), cashierStationId, cashierStation.Name, areaId, numberToCall);

                // Prepare DTOs
                var broadcastDto = new CalledNumberBroadcastDto
                {
                    AreaId = areaId,
                    TicketNumber = numberToCall,
                    CashierStationId = cashierStationId,
                    CashierStationName = cashierStation.Name,
                    Timestamp = queueState.LastCallTimestamp.Value // Use the timestamp saved to DB
                };

                var responseDto = new CalledNumberDto
                {
                    TicketNumber = numberToCall,
                    CashierStationId = cashierStationId,
                    CashierStationName = cashierStation.Name
                };

                // Broadcast the update
                await BroadcastQueueUpdateAsync(areaId, broadcastDto);

                return ServiceResult<CalledNumberDto>.Ok(responseDto);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Log the error and inform the user. They might need to retry.
                 _logger.LogError(ex, "Concurrency error calling next number for AreaId {AreaId} by Station {StationId}. Queue state may have changed.", areaId, cashierStationId);
                return ServiceResult<CalledNumberDto>.Fail("Could not call next number due to a conflict. Please try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling next number for AreaId {AreaId} by Station {StationId}", areaId, cashierStationId);
                return ServiceResult<CalledNumberDto>.Fail("An unexpected error occurred while calling the next number.");
            }
            // --- End Critical Section ---
        }

        public async Task<ServiceResult<CalledNumberDto>> CallSpecificAsync(int areaId, int cashierStationId, int ticketNumber)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            if (!isSuperAdmin && !userOrgId.HasValue)
            {
                return ServiceResult<CalledNumberDto>.Fail("User organization context could not be determined.");
            }

            if (ticketNumber < 1)
            {
                return ServiceResult<CalledNumberDto>.Fail("Ticket number must be 1 or greater.");
            }

            // Verify CashierStation exists, is enabled, and belongs to the correct Area and Organization
            var cashierStation = await _context.CashierStations
                                                .AsNoTracking()
                                                .FirstOrDefaultAsync(cs => cs.Id == cashierStationId && cs.IsEnabled);
            
            if (cashierStation == null)
            {
                return ServiceResult<CalledNumberDto>.Fail($"Cashier station with ID {cashierStationId} not found or is disabled.");
            }
            if (cashierStation.AreaId != areaId)
            {
                _logger.LogWarning("Cashier station {CashierStationId} in Area {StationAreaId} used to call specific for Area {AreaId}", cashierStationId, cashierStation.AreaId, areaId);
                return ServiceResult<CalledNumberDto>.Fail("Cashier station does not belong to the specified area.");
            }
             if (!isSuperAdmin && cashierStation.OrganizationId != userOrgId)
            {
                _logger.LogWarning("User from Org {UserOrgId} attempted to use CashierStation {CashierStationId} in Org {StationOrgId}", userOrgId, cashierStationId, cashierStation.OrganizationId);
                return ServiceResult<CalledNumberDto>.Fail($"Cashier station with ID {cashierStationId} not found."); // Mask as not found
            }

            // Use the helper to get/create queue state
            var queueState = await GetOrCreateQueueStateAsync(areaId);
             if (queueState == null)
            {
                 return ServiceResult<CalledNumberDto>.Fail($"Could not access queue state for Area ID {areaId}.");
            }

            // Ensure queue system is enabled for the area
            var area = await _context.Areas.AsNoTracking().FirstOrDefaultAsync(a => a.Id == areaId);
            if (area == null || !area.EnableQueueSystem)
            {
                 _logger.LogWarning("Attempted to call specific for AreaId {AreaId} where queue system is disabled.", areaId);
                 return ServiceResult<CalledNumberDto>.Fail("Queue system is not enabled for this area.");
            }

            // Calling a specific number updates the 'Last Called' information.
            // AND it should set the NextSequentialNumber to be the called number + 1.
            queueState.LastCalledNumber = ticketNumber;
            queueState.LastCalledCashierStationId = cashierStationId;
            queueState.LastCallTimestamp = DateTime.UtcNow;
            queueState.NextSequentialNumber = ticketNumber + 1; // Update NextSequentialNumber

             _context.AreaQueueStates.Update(queueState);

             try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("User {UserId} at Station {StationId} ({StationName}) called specific number {TicketNumber} for Area {AreaId}", GetUserId(), cashierStationId, cashierStation.Name, ticketNumber, areaId);

                // Prepare DTOs
                var broadcastDto = new CalledNumberBroadcastDto
                {
                    AreaId = areaId,
                    TicketNumber = ticketNumber,
                    CashierStationId = cashierStationId,
                    CashierStationName = cashierStation.Name,
                    Timestamp = queueState.LastCallTimestamp.Value
                };

                var responseDto = new CalledNumberDto
                {
                    TicketNumber = ticketNumber,
                    CashierStationId = cashierStationId,
                    CashierStationName = cashierStation.Name
                };

                // Broadcast the update
                await BroadcastQueueUpdateAsync(areaId, broadcastDto);

                return ServiceResult<CalledNumberDto>.Ok(responseDto);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                 _logger.LogError(ex, "Concurrency error calling specific number {TicketNumber} for AreaId {AreaId} by Station {StationId}.", ticketNumber, areaId, cashierStationId);
                return ServiceResult<CalledNumberDto>.Fail("Could not call specific number due to a conflict. Please try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling specific number {TicketNumber} for AreaId {AreaId} by Station {StationId}", ticketNumber, areaId, cashierStationId);
                return ServiceResult<CalledNumberDto>.Fail("An unexpected error occurred while calling the specific number.");
            }
        }

        public async Task<ServiceResult<QueueStateDto>> GetQueueStateAsync(int areaId)
        {
            // First, get the Area itself to check existence and authorization
            var area = await _context.Areas.AsNoTracking().FirstOrDefaultAsync(a => a.Id == areaId);

            if (area == null)
            {
                return ServiceResult<QueueStateDto>.Fail($"Area with ID {areaId} not found.");
            }

            // If queue system is disabled for the area, return a specific state
            if (!area.EnableQueueSystem)
            {
                return ServiceResult<QueueStateDto>.Ok(new QueueStateDto
                {
                    AreaId = area.Id,
                    IsQueueSystemEnabled = false
                    // Other fields remain default (0, null)
                });
            }

            // Now query the AreaQueueState, including the related CashierStation
            var queueState = await _context.AreaQueueStates
                                        .Include(qs => qs.LastCalledCashierStation) // Include station details
                                        .AsNoTracking()
                                        .FirstOrDefaultAsync(qs => qs.AreaId == areaId);


            if (queueState == null)
            {
                _logger.LogWarning("AreaQueueState is null for enabled AreaId {AreaId}. Returning defaults.", areaId);
                // Return default state for an enabled system without an existing state record
                return ServiceResult<QueueStateDto>.Ok(new QueueStateDto
                {
                    AreaId = area.Id, // Use areaId from the validated Area object
                    IsQueueSystemEnabled = true,
                    NextSequentialNumber = 1, // Default starting point
                    LastCalledNumber = null,
                    LastCalledCashierStationId = null,
                    LastCalledCashierStationName = null,
                    LastCallTimestamp = null,
                    LastResetTimestamp = null
                });
            }

            // Map the found AreaQueueState entity to the DTO
            var stateDto = new QueueStateDto
            {
                AreaId = area.Id, // Use areaId from the validated Area object
                IsQueueSystemEnabled = true, // We already checked area.EnableQueueSystem
                NextSequentialNumber = queueState.NextSequentialNumber,
                LastCalledNumber = queueState.LastCalledNumber,
                LastCalledCashierStationId = queueState.LastCalledCashierStationId,
                LastCalledCashierStationName = queueState.LastCalledCashierStation?.Name, // Get name from included nav property
                LastCallTimestamp = queueState.LastCallTimestamp,
                LastResetTimestamp = queueState.LastResetTimestamp
            };

            return ServiceResult<QueueStateDto>.Ok(stateDto);
        }

        public async Task<ServiceResult> ResetQueueAsync(int areaId, int startingNumber = 1)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            if (!isSuperAdmin && !userOrgId.HasValue)
            {
                return ServiceResult.Fail("User organization context could not be determined.");
            }

            // Role check: Only SuperAdmin or OrgAdmin should be able to reset
            var user = _httpContextAccessor.HttpContext?.User;
            if (!isSuperAdmin && !(user?.IsInRole("OrgAdmin") ?? false))
            {
                return ServiceResult.Fail("User is not authorized to reset the queue.");
            }

             if (startingNumber < 1)
            {
                return ServiceResult.Fail("Starting number must be 1 or greater.");
            }

            // Use the helper to ensure state exists and check area access
            var queueState = await GetOrCreateQueueStateAsync(areaId);
            if (queueState == null)
            {
                // GetOrCreateQueueStateAsync handles logging and auth checks
                return ServiceResult.Fail($"Could not access or create queue state for Area ID {areaId}. Area may not exist or user lacks permission.");
            }

            // Check if queue system is actually enabled for the area (query Area directly)
            var area = await _context.Areas.AsNoTracking().FirstOrDefaultAsync(a => a.Id == areaId);
            if (area == null || !area.EnableQueueSystem)
            {
                 _logger.LogWarning("Attempted to reset queue for AreaId {AreaId} where queue system is disabled.", areaId);
                 return ServiceResult.Fail("Queue system is not enabled for this area.");
            }

            // Perform the reset
            queueState.NextSequentialNumber = startingNumber;
            queueState.LastCalledNumber = null; // Reset last called info
            queueState.LastCalledCashierStationId = null;
            queueState.LastCallTimestamp = null;
            queueState.LastResetTimestamp = DateTime.UtcNow;

            _context.AreaQueueStates.Update(queueState);

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Queue for AreaId {AreaId} reset to {StartingNumber} by User {UserId}", areaId, startingNumber, GetUserId());

                // Broadcast the reset event
                await BroadcastQueueResetAsync(areaId);

                // Optionally broadcast the new state
                // var newStateDto = await GetQueueStateAsync(areaId); // Re-fetch the DTO
                // if(newStateDto.Success) await BroadcastQueueStateAsync(areaId, newStateDto.Value!);

                return ServiceResult.Ok();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                 _logger.LogError(ex, "Concurrency error while resetting queue for AreaId: {AreaId}", areaId);
                return ServiceResult.Fail("Could not reset queue due to a concurrency conflict. Please try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting queue for AreaId: {AreaId}", areaId);
                return ServiceResult.Fail("An unexpected error occurred while resetting the queue.");
            }
        }

        public async Task<ServiceResult> UpdateNextSequentialNumberAsync(int areaId, int newNextNumber)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            if (!isSuperAdmin && !userOrgId.HasValue)
            {
                return ServiceResult.Fail("User organization context could not be determined.");
            }

            // Role check: Only SuperAdmin or OrgAdmin should be able to do this
            var user = _httpContextAccessor.HttpContext?.User;
            if (!isSuperAdmin && !(user?.IsInRole("OrgAdmin") ?? false))
            {
                return ServiceResult.Fail("User is not authorized to update the next sequential number.");
            }

            if (newNextNumber < 1)
            {
                return ServiceResult.Fail("New next sequential number must be 1 or greater.");
            }

            // Use the helper to ensure state exists and check area access
            var queueState = await GetOrCreateQueueStateAsync(areaId);
            if (queueState == null)
            {
                 return ServiceResult.Fail($"Could not access or create queue state for Area ID {areaId}. Area may not exist or user lacks permission.");
            }

            // Check if queue system is actually enabled for the area
            var area = await _context.Areas.AsNoTracking().FirstOrDefaultAsync(a => a.Id == areaId);
             if (area == null || !area.EnableQueueSystem)
            {
                 _logger.LogWarning("Attempted to update next sequential for AreaId {AreaId} where queue system is disabled.", areaId);
                 return ServiceResult.Fail("Queue system is not enabled for this area.");
            }

            queueState.NextSequentialNumber = newNextNumber;
            _context.AreaQueueStates.Update(queueState);

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Next sequential number for AreaId {AreaId} updated to {NewNextNumber} by User {UserId}", areaId, newNextNumber, GetUserId());

                // Optionally broadcast the new state (consider if this is too noisy or if QueueStateUpdated is preferred)
                var currentState = await GetQueueStateAsync(areaId); // Re-fetch to get full DTO
                if (currentState.Success)
                {
                    await BroadcastQueueStateAsync(areaId, currentState.Value!); // Send full state DTO
                }
                
                return ServiceResult.Ok();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error while updating next sequential number for AreaId: {AreaId}", areaId);
                return ServiceResult.Fail("Could not update next sequential number due to a concurrency conflict. Please try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating next sequential number for AreaId: {AreaId}", areaId);
                return ServiceResult.Fail("An unexpected error occurred while updating the next sequential number.");
            }
        }

        public async Task<ServiceResult> ToggleQueueSystemAsync(int areaId, bool enable)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            if (!isSuperAdmin && !userOrgId.HasValue)
            {
                return ServiceResult.Fail("User organization context could not be determined.");
            }

            // Role check: Only SuperAdmin or OrgAdmin should be able to toggle this
            var user = _httpContextAccessor.HttpContext?.User;
            if (!isSuperAdmin && !(user?.IsInRole("OrgAdmin") ?? false))
            {
                return ServiceResult.Fail("User is not authorized to manage queue system settings.");
            }

            var area = await _context.Areas.FindAsync(areaId);

            if (area == null)
            {
                return ServiceResult.Fail($"Area with ID {areaId} not found.");
            }

            if (!isSuperAdmin && area.OrganizationId != userOrgId)
            {
                _logger.LogWarning("User from Org {UserOrgId} attempted to toggle queue for Area {AreaId} in Org {AreaOrgId}", userOrgId, areaId, area.OrganizationId);
                return ServiceResult.Fail("User is not authorized to modify this Area."); // Or a more generic not found
            }

            area.EnableQueueSystem = enable;
            _context.Areas.Update(area);

            try
            {
                await _context.SaveChangesAsync();
                _logger.LogInformation("Queue system for AreaId {AreaId} set to {Status} by User {UserId}", areaId, enable, GetUserId());
                // Optionally broadcast a system status update if display screens need to know
                // await BroadcastQueueSystemStatusChangedAsync(areaId, enable);
                return ServiceResult.Ok();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogError(ex, "Concurrency error while toggling queue system for AreaId: {AreaId}", areaId);
                return ServiceResult.Fail("Could not update area queue settings due to a concurrency conflict. Please try again.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling queue system for AreaId: {AreaId}", areaId);
                return ServiceResult.Fail("An unexpected error occurred while updating area queue settings.");
            }
        }

        // --- Publicly Accessible Methods (Minimal Authorization) ---

        public async Task<ServiceResult<List<CashierStationDto>>> GetActiveCashierStationsForAreaAsync(int areaId)
        {
            // Basic validation: Check if Area exists
            var areaExists = await _context.Areas.AsNoTracking().AnyAsync(a => a.Id == areaId);
            if (!areaExists)
            {
                _logger.LogWarning("Attempt to get active cashier stations for non-existent AreaId: {AreaId}", areaId);
                return ServiceResult<List<CashierStationDto>>.Fail("Area not found.");
            }

            // Fetch enabled cashier stations for the given areaId
            var stations = await _context.CashierStations
                .AsNoTracking()
                .Where(cs => cs.AreaId == areaId && cs.IsEnabled)
                .Select(cs => new CashierStationDto
                {
                    Id = cs.Id,
                    OrganizationId = cs.OrganizationId,
                    AreaId = cs.AreaId,
                    Name = cs.Name,
                    IsEnabled = cs.IsEnabled,
                    // Map other relevant properties from CashierStation to CashierStationDto if needed for public display
                    // For qDisplay, Id and Name are primary. AreaId and IsEnabled are good for consistency.
                })
                .ToListAsync();

            if (stations == null) // Should be an empty list if none found, but defensive check
            {
                _logger.LogInformation("No active cashier stations found for AreaId: {AreaId}, returning empty list.", areaId);
                return ServiceResult<List<CashierStationDto>>.Ok(new List<CashierStationDto>());
            }

            return ServiceResult<List<CashierStationDto>>.Ok(stations);
        }

        // --- Private Helper Methods (e.g., for broadcasting) ---
        private async Task BroadcastQueueUpdateAsync(int areaId, CalledNumberBroadcastDto dto)
        {
            var groupName = $"Area-{areaId}";
            try
            {
                await _hubContext.Clients.Group(groupName).SendAsync("QueueNumberCalled", dto);
                _logger.LogInformation("Broadcast QueueNumberCalled to group {GroupName}: Ticket {TicketNumber} -> Station {StationName}", groupName, dto.TicketNumber, dto.CashierStationName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting QueueNumberCalled to group {GroupName}", groupName);
            }
        }

        private async Task BroadcastQueueResetAsync(int areaId)
        {
            var groupName = $"Area-{areaId}";
            try
            {
                var timestamp = DateTime.UtcNow;
                await _hubContext.Clients.Group(groupName).SendAsync("QueueReset", areaId, timestamp);
                _logger.LogInformation("Broadcast QueueReset to group {GroupName} at {Timestamp}", groupName, timestamp);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting QueueReset to group {GroupName}", groupName);
            }
        }

         // Optional: Broadcast full state if needed
        private async Task BroadcastQueueStateAsync(int areaId, QueueStateDto stateDto)
        {
             var groupName = $"Area-{areaId}";
             try
             {
                 await _hubContext.Clients.Group(groupName).SendAsync("QueueStateUpdated", stateDto);
                _logger.LogInformation("Broadcast QueueStateUpdated to group {GroupName}", groupName);
             }
             catch (Exception ex)
             {
                 _logger.LogError(ex, "Error broadcasting QueueStateUpdated to group {GroupName}", groupName);
             }
        }

        // Helper to get or create queue state, handling potential concurrency
        private async Task<AreaQueueState?> GetOrCreateQueueStateAsync(int areaId)
        {
            var queueState = await _context.AreaQueueStates.FirstOrDefaultAsync(q => q.AreaId == areaId);
            if (queueState == null)
            {
                // Check if Area exists and belongs to the user's org
                var (userOrgId, isSuperAdmin) = GetUserContext();
                var area = await _context.Areas.FindAsync(areaId);

                if (area == null)
                {
                    _logger.LogWarning("Attempted to get/create queue state for non-existent AreaId: {AreaId}", areaId);
                    return null; // Area not found
                }
                if (!isSuperAdmin && area.OrganizationId != userOrgId)
                {
                    _logger.LogWarning("User attempted to access queue state for AreaId {AreaId} in different organization {OrgId}", areaId, area.OrganizationId);
                    return null; // Authorization fail
                }

                // Create a new state
                queueState = new AreaQueueState
                {
                    AreaId = areaId,
                    NextSequentialNumber = 1 // Default starting number
                };
                _context.AreaQueueStates.Add(queueState);

                try
                {
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("Created new AreaQueueState for AreaId: {AreaId}", areaId);
                }
                 catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23505") // Unique constraint violation
                {
                    _logger.LogWarning("Concurrency conflict creating AreaQueueState for AreaId: {AreaId}. Reloading existing.", areaId);
                    // Another request likely created it just now, reload it
                    _context.Entry(queueState).State = EntityState.Detached; // Detach the failed new entity
                    queueState = await _context.AreaQueueStates.FirstOrDefaultAsync(q => q.AreaId == areaId);
                    if (queueState == null)
                    { 
                        _logger.LogError("Failed to reload AreaQueueState for AreaId {AreaId} after concurrency conflict.", areaId);
                        throw; // Re-throw if it's still null, something is wrong
                    }
                }
                 catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving new AreaQueueState for AreaId: {AreaId}", areaId);
                    throw; // Re-throw other exceptions
                }
            }
            return queueState;
        }

        public async Task<ServiceResult<CalledNumberDto>> RespeakLastCalledNumberAsync(int areaId, int cashierStationId)
        {
            var (userOrgId, isSuperAdmin) = GetUserContext();
            if (!isSuperAdmin && !userOrgId.HasValue)
            {
                return ServiceResult<CalledNumberDto>.Fail("User organization context could not be determined.");
            }

            // Verify CashierStation exists, is enabled, and belongs to the correct Area and Organization
            var cashierStation = await _context.CashierStations
                                                .AsNoTracking()
                                                .FirstOrDefaultAsync(cs => cs.Id == cashierStationId && cs.IsEnabled);

            if (cashierStation == null)
            {
                return ServiceResult<CalledNumberDto>.Fail($"Cashier station with ID {cashierStationId} not found or is disabled.");
            }
            if (cashierStation.AreaId != areaId)
            {
                _logger.LogWarning("Cashier station {CashierStationId} in Area {StationAreaId} used to respeak for Area {AreaId}", cashierStationId, cashierStation.AreaId, areaId);
                return ServiceResult<CalledNumberDto>.Fail("Cashier station does not belong to the specified area.");
            }
            if (!isSuperAdmin && cashierStation.OrganizationId != userOrgId)
            {
                _logger.LogWarning("User from Org {UserOrgId} attempted to use CashierStation {CashierStationId} in Org {StationOrgId} for respeak", userOrgId, cashierStationId, cashierStation.OrganizationId);
                return ServiceResult<CalledNumberDto>.Fail($"Cashier station with ID {cashierStationId} not found."); // Mask as not found
            }

            // Get the current queue state
            var queueState = await _context.AreaQueueStates
                                        .AsNoTracking()
                                        .FirstOrDefaultAsync(qs => qs.AreaId == areaId);

            if (queueState == null)
            {
                _logger.LogWarning("AreaQueueState not found for AreaId {AreaId} during respeak request.", areaId);
                return ServiceResult<CalledNumberDto>.Fail("Queue state not found for this area. Cannot respeak.");
            }
            
            var area = await _context.Areas.AsNoTracking().FirstOrDefaultAsync(a => a.Id == areaId);
            if (area == null || !area.EnableQueueSystem)
            {
                 _logger.LogWarning("Attempted to respeak for AreaId {AreaId} where queue system is disabled.", areaId);
                 return ServiceResult<CalledNumberDto>.Fail("Queue system is not enabled for this area.");
            }

            if (queueState.LastCalledNumber == null || queueState.LastCallTimestamp == null)
            {
                _logger.LogInformation("No number has been called yet for AreaId {AreaId}. Nothing to respeak.", areaId);
                return ServiceResult<CalledNumberDto>.Fail("No number has been called yet for this area.");
            }
            
            // We need the name of the station that made the *original* call, which might be different from the requesting station.
            // However, for simplicity and to ensure the respeak comes from the *current* station context if that's desired,
            // we will use the requesting cashierStation's name for the broadcast.
            // If the requirement was to always announce the *original* calling station, we'd need to store that with LastCalledNumber.
            // For now, the respeak is attributed to the station making the respeak request.

            var lastCalledStationIdForBroadcast = queueState.LastCalledCashierStationId ?? cashierStationId; // Fallback to current if original not stored
            var lastCalledStationNameForBroadcast = cashierStation.Name; // Attribute respeak to current station

            // If we want to use the original station that called the number:
            // var originalCallingStation = await _context.CashierStations.AsNoTracking().FirstOrDefaultAsync(cs => cs.Id == queueState.LastCalledCashierStationId);
            // lastCalledStationNameForBroadcast = originalCallingStation?.Name ?? cashierStation.Name; // Fallback if original not found


            _logger.LogInformation("User {UserId} at Station {StationId} ({StationName}) requested respeak of number {LastCalledNumber} for Area {AreaId}", GetUserId(), cashierStationId, cashierStation.Name, queueState.LastCalledNumber, areaId);

            var broadcastDto = new CalledNumberBroadcastDto
            {
                AreaId = areaId,
                TicketNumber = queueState.LastCalledNumber.Value,
                CashierStationId = lastCalledStationIdForBroadcast, // Use the station ID that made the last call
                CashierStationName = lastCalledStationNameForBroadcast, // Use the name of the station that made the last call (or current if preferred)
                Timestamp = queueState.LastCallTimestamp.Value // Use the original call timestamp
            };

            var responseDto = new CalledNumberDto
            {
                TicketNumber = queueState.LastCalledNumber.Value,
                CashierStationId = lastCalledStationIdForBroadcast,
                CashierStationName = lastCalledStationNameForBroadcast
            };

            // Broadcast the update
            await BroadcastQueueUpdateAsync(areaId, broadcastDto); // This sends "QueueNumberCalled"

            return ServiceResult<CalledNumberDto>.Ok(responseDto);
        }
    }
}

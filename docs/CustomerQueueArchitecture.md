Okay, here is the updated architecture document incorporating the `CashierStation` linkage.

---

**Architecture Document: Customer Queue Management System for SagraFacile**

**1. Introduction & Goals**

*   **1.1. Purpose:** To implement a queue management system integrated into SagraFacile, allowing customers with numbered tickets (obtained externally) to be called to specific cashier stations within an area.
*   **1.2. Goals:**
    *   Provide a mechanism for cashiers to call the next sequential ticket number for their Area, associating the call with their specific `CashierStation`.
    *   Allow cashiers to call a specific ticket number, associating it with their station.
    *   Display the called ticket number and the assigned `CashierStation` name on a public-facing screen for the Area.
    *   Announce the called number and assigned station via audio from the public display screen's host PC.
    *   Manage the queue sequence (`NextSequentialNumber`) at the `Area` level.
    *   Allow administrators to enable/disable the queue system per `Area` and reset the sequence.
    *   Integrate seamlessly with the existing SagraFacile architecture (Backend: .NET, Frontend: Next.js, Database: PostgreSQL, Real-time: SignalR).
*   **1.3. Non-Goals (for initial version):**
    *   Ticket printing/dispensing mechanism within SagraFacile.
    *   Complex queue logic (e.g., priority queues, multiple queue types per area).
    *   Estimating wait times.
    *   Visual differentiation between "calling" and "now serving" states on the public display (display shows latest calls).

**2. Proposed Solution Overview**

*   **2.1. Core Concept:** A shared queue sequence is maintained per `Area`. Individual `CashierStation` interfaces within that Area trigger calls for the next number (or a specific number). These calls are linked to the initiating `CashierStation`. A central display screen for the `Area` subscribes to real-time updates, showing the latest calls (ticket number and station name) and providing audio announcements.
*   **2.2. Key Components:**
    *   **Queue Display Screen:** A new, publicly accessible Next.js page (e.g., `/qdisplay/org/{orgId}/area/{areaId}`) showing a list of recent calls (Ticket Number -> Station Name). Connects via SignalR and handles audio announcements.
    *   **Cashier Interface Enhancements:** Modifications to the existing cashier page (`/app/app/org/[orgId]/cashier/area/[areaId]/page.tsx`) to display queue state and provide buttons/inputs for calling numbers, sending the active `cashierStationId` with requests.
    *   **Backend Logic:** New services (`QueueService`), API endpoints (`QueueController`), database entities (`AreaQueueState`), and SignalR messages to manage queue state, handle call requests, and broadcast updates.
    *   **Admin Interface Enhancements:** Toggle in Area settings to enable/disable the queue feature. Optional dedicated admin page for queue reset/management.

**3. Detailed Architecture**

    **3.1. Backend (.NET API - SagraFacile.NET)**
        *   **3.1.1. Database Changes (Entity Framework Core / PostgreSQL):**
            *   **New Entity: `AreaQueueState`** (SagraFacile.NET.API/Models/AreaQueueState.cs)
                ```csharp
                using System.ComponentModel.DataAnnotations;
                using System.ComponentModel.DataAnnotations.Schema;

                namespace SagraFacile.NET.API.Models
                {
                    public class AreaQueueState
                    {
                        [Key]
                        public int Id { get; set; } // Or Guid if preferred

                        [Required]
                        public int AreaId { get; set; }
                        [ForeignKey("AreaId")]
                        public Area Area { get; set; } // Ensure Unique Constraint on AreaId

                        [Required]
                        public int NextSequentialNumber { get; set; } = 1; // The next number to be called by "Call Next"

                        // Last successful call details
                        public int? LastCalledNumber { get; set; }
                        public int? LastCalledCashierStationId { get; set; } // Nullable FK
                        [ForeignKey("LastCalledCashierStationId")]
                        public CashierStation LastCalledCashierStation { get; set; }
                        public DateTime? LastCallTimestamp { get; set; }

                        public DateTime? LastResetTimestamp { get; set; }
                    }
                }
                ```
                *   Add `DbSet<AreaQueueState>` to `ApplicationDbContext`.
                *   Configure a unique index on `AreaQueueState.AreaId`.
                *   Configure the nullable foreign key relationship to `CashierStation`.
                *   Generate and apply EF Core migration (`dotnet ef migrations add AddQueueManagement`).
            *   **Modifications to `Area` Entity:** (SagraFacile.NET.API/Models/Area.cs)
                ```csharp
                // Inside public class Area { ... }
                [Required]
                public bool EnableQueueSystem { get; set; } = false;
                ```
                *   Add this property, update migrations if necessary.
        *   **3.1.2. New Service: `IQueueService` / `QueueService.cs`** (SagraFacile.NET.API/Services/)
            *   Requires injection of `ApplicationDbContext`, `IHubContext<OrderHub>` (or dedicated `QueueHub`), `ILogger`.
            *   `Task<CalledNumberDto> CallNextAsync(int areaId, int cashierStationId)`:
                *   Verify `Area.EnableQueueSystem` is true.
                *   Fetch `CashierStation` entity using `cashierStationId` to validate it and get its `Name`.
                *   Retrieve or create `AreaQueueState` for `areaId`.
                *   Atomically get `NextSequentialNumber` (handle concurrency if needed).
                *   Update `AreaQueueState` (increment `NextSequentialNumber`, set `LastCalledNumber = retrievedNumber`, `LastCalledCashierStationId = cashierStationId`, `LastCallTimestamp = DateTime.UtcNow`). Save changes.
                *   Prepare `CalledNumberBroadcastDto` (including `AreaId`, `TicketNumber`, `CashierStationId`, `CashierStationName`, `Timestamp`).
                *   Broadcast DTO via SignalR hub to clients interested in this `AreaId`.
                *   Return `CalledNumberDto` (containing ticket number, station details) to the calling cashier's API request.
            *   `Task<CalledNumberDto> CallSpecificAsync(int areaId, int cashierStationId, int ticketNumber)`:
                *   Similar to `CallNextAsync`, but uses the provided `ticketNumber`.
                *   Fetch `CashierStation` entity.
                *   Update `AreaQueueState` (`LastCalledNumber`, `LastCalledCashierStationId`, `LastCallTimestamp`) *only if* this call is relevant (e.g., newer timestamp or higher number than current last call). Does *not* usually increment `NextSequentialNumber`.
                *   Prepare and broadcast `CalledNumberBroadcastDto`.
                *   Return `CalledNumberDto`.
            *   `Task<QueueStateDto> GetQueueStateAsync(int areaId)`:
                *   Returns current `NextSequentialNumber`, `LastCalledNumber`, `LastCalledCashierStationId`, `LastCalledCashierStation.Name` (requires Include) etc. for an Area.
            *   `Task ResetQueueAsync(int areaId, int startingNumber = 1)`:
                *   Verify permissions (Admin/AreaManager).
                *   Find `AreaQueueState`.
                *   Update `NextSequentialNumber = startingNumber`, `LastResetTimestamp = DateTime.UtcNow`. (Consider clearing LastCalled fields). Save changes.
                *   Broadcast a `QueueReset(areaId, DateTime.UtcNow)` message via SignalR.
            *   `Task UpdateNextSequentialNumberAsync(int areaId, int newNextNumber)`: (Admin protected)
                *   Directly sets the `NextSequentialNumber`. Use with caution. Save changes.
                *   Consider broadcasting `QueueStateUpdated`.
        *   **3.1.3. API Endpoints (New Controller: `QueueController.cs`)** (SagraFacile.NET.API/Controllers/)
            *   `[Authorize]` attribute on controller.
            *   Inject `IQueueService`.
            *   `POST /api/areas/{areaId}/queue/call-next` (Requires `cashierStationId` in body or query string) - Returns `Task<ActionResult<CalledNumberDto>>`.
            *   `POST /api/areas/{areaId}/queue/call-specific` (Requires `cashierStationId`, `ticketNumber` in body or query string) - Returns `Task<ActionResult<CalledNumberDto>>`.
            *   `GET /api/areas/{areaId}/queue/state` - Returns `Task<ActionResult<QueueStateDto>>`.
            *   `POST /api/areas/{areaId}/queue/reset` (Requires `startingNumber` optional param. Needs Admin/AreaManager role check) - Returns `Task<IActionResult>`.
            *   `PUT /api/areas/{areaId}/queue/next-sequential-number` (Requires `newNextNumber`. Needs Admin/AreaManager role check) - Returns `Task<IActionResult>`.
        *   **3.1.4. SignalR (`OrderHub.cs` is suitable, no need for a new Hub initially)** (SagraFacile.NET.API/Hubs/)
            *   Modify Hub logic if needed for group management based on Area interest (clients join `Area-{areaId}` groups).
            *   **New Message Contracts:**
                *   `QueueNumberCalled(CalledNumberBroadcastDto data)`
                *   `QueueReset(int areaId, DateTime timestamp)`
                *   *(Optional)* `QueueStateUpdated(QueueStateDto data)`
            *   **Broadcasting:** `QueueService` uses `_hubContext.Clients.Group($"Area-{areaId}").SendAsync("QueueNumberCalled", dto);`
        *   **3.1.5. DTOs:** (SagraFacile.NET.API/DTOs/)
            *   `CalledNumberDto`: { int TicketNumber, int CashierStationId, string CashierStationName } - For API responses to cashier.
            *   `CalledNumberBroadcastDto`: { int AreaId, int TicketNumber, int CashierStationId, string CashierStationName, DateTime Timestamp } - For SignalR broadcast.
            *   `CalledNumberBroadcastDto`: { int AreaId, int TicketNumber, int CashierStationId, string CashierStationName, DateTime Timestamp } - For SignalR broadcast.
            *   `QueueStateDto`: { int AreaId, int NextSequentialNumber, int? LastCalledNumber, int? LastCalledCashierStationId, string LastCalledCashierStationName, DateTime? LastCallTimestamp, DateTime? LastResetTimestamp }

    **3.2. Frontend (Next.js App - sagrafacile-webapp)**
        *   **3.2.1. Cashier Interface (`/app/app/org/[orgId]/cashier/area/[areaId]/page.tsx`)**
            *   **Prerequisite:** The page must already have the selected `cashierStationId` available in its state (as established in previous work).
            *   **Conditional Rendering:** Only show queue elements if `AreaDto.enableQueueSystem` is true.
            *   **UI Elements:**
                *   Display: "Area Queue Next: [NextSequentialNumber]" (Fetched via `GET /api/areas/{areaId}/queue/state` or SignalR `QueueStateUpdated`).
                *   Display: "Your Last Call: Ticket [TicketNumber]" (Updated from API response of call actions).
                *   Button: "CALL NEXT TICKET" (`<Button />`)
                *   Input field for ticket number (`<Input type="number" />`)
                *   Button: "CALL SPECIFIC TICKET" (`<Button />`)
            *   **Logic:**
                *   Fetch initial queue state on load if queue system is enabled.
                *   Connect to SignalR and join `Area-{areaId}` group. Listen for `QueueStateUpdated` (if implemented) to update "Area Queue Next".
                *   "CALL NEXT TICKET" click handler:
                    *   Show loading state.
                    *   Call `apiClient.post(`/areas/${areaId}/queue/call-next`, { cashierStationId: selectedCashierStationId })`.
                    *   On success, update "Your Last Call" display using the response `CalledNumberDto`. Subsequently, real-time updates (including those initiated by this cashier's actions) should reflect through SignalR messages updating the shared `queueState`.
                    *   Handle errors.
                *   "CALL SPECIFIC TICKET" click handler:
                    *   Get `ticketNumber` from input. Validate it.
                    *   Show loading state.
                    *   Call `apiClient.post(`/areas/${areaId}/queue/call-specific`, { cashierStationId: selectedCashierStationId, ticketNumber: ticketNumber })`.
                    *   On success, update "Your Last Call". Subsequently, real-time updates (including those initiated by this cashier's actions) should reflect through SignalR messages updating the shared `queueState`.
                    *   Handle errors.
        *   **3.2.2. New Queue Display Page (`/qdisplay/org/{orgId}/area/{areaId}` - public)**
            *   **Routing:** Add this route (can be outside the `/app` structure if no auth needed).
            *   **UI Elements:**
                *   Large Heading: "NOW SERVING - [Area Name]" (Fetch Area details).
                *   List/Table display for the last N (e.g., 5-7) calls. Maintain state `recentCalls: CalledNumberBroadcastDto[]`.
                *   Render list: `recentCalls.map(call => <div>TICKET {call.TicketNumber} &rarr; CASHIER {call.CashierStationName}</div>)` (Newest at top).
            *   **Logic:**
                *   Get `areaId` from URL params.
                *   Fetch initial Area details (for name, and to check if `enableQueueSystem` is true). If not enabled, show message.
                *   Connect to SignalR hub (`useSignalRHub` hook). Join `Area-{areaId}` group.
                *   Listen for `QueueNumberCalled` messages:
                    *   On message (`data: CalledNumberBroadcastDto`):
                        *   Update `recentCalls` state: `setRecentCalls(prev => [data, ...prev].slice(0, 7))`.
                        *   Trigger Text-to-Speech:
                            ```javascript
                            if ('speechSynthesis' in window) {
                                const utterance = new SpeechSynthesisUtterance(`Number ${data.TicketNumber}, please go to Cashier ${data.CashierStationName}`);
                                // Optional: Configure voice, rate, pitch here
                                // const voices = window.speechSynthesis.getVoices();
                                // utterance.voice = voices.find(v => v.lang === 'it-IT'); // Example
                                window.speechSynthesis.speak(utterance);
                            } else {
                                console.warn("Browser does not support Speech Synthesis.");
                            }
                            ```
                *   Listen for `QueueReset` messages:
                    *   On message: `setRecentCalls([])`. Optionally display "Queue Reset".
                *   Handle SignalR connection errors/disconnects.
        *   **3.2.3. Admin Interface (`/app/app/org/[orgId]/admin/areas/page.tsx`)**
            *   In the "Edit Area" dialog (`AreaFormDialog` or similar):
                *   Add a `Switch` component bound to the `enableQueueSystem` property of the `AreaDto`.
                *   Update the `handleEditArea` function to include this flag in the `AreaUpsertDto` sent to the backend.
        *   **3.2.4. (Optional) New Admin Queue Management Page (`/app/app/org/[orgId]/admin/queue-management/area/{areaId}`)**
            *   Requires Admin/AreaManager role.
            *   Fetch and display `QueueStateDto` using `queueService.getQueueState(areaId)`.
            *   Button "Reset Queue": Opens a confirmation dialog, then calls `queueService.resetQueue(areaId, startingNumber)`.
            *   Input + Button "Set Next Sequential Number": Calls `queueService.updateNextSequentialNumber(areaId, newNumber)`.
        *   **3.2.5. New Services/Hooks (`src/services/queueService.ts`)**
            *   Create functions mirroring API client calls for `callNext`, `callSpecific`, `getState`, `resetQueue`, `updateNextNumber`.
            *   Update `useSignalRHub` if needed to handle specific message types or group joining logic robustly.
        *   **3.2.6. Types (`src/types/index.ts`)**
            *   Add TypeScript interfaces mirroring backend DTOs: `AreaQueueStateDto`, `CalledNumberDto`, `CalledNumberBroadcastDto`.
            *   Update `AreaDto` interface to include `enableQueueSystem: boolean`.

**4. Data Flow Examples**

*   **4.1. Cashier Calls Next Ticket:**
    1.  Cashier at Station "Cassa 2" (`cashierStationId: 5`) clicks "CALL NEXT TICKET".
    2.  Frontend sends `POST /api/areas/1/queue/call-next` with body `{ cashierStationId: 5 }`.
    3.  Backend `QueueService.CallNextAsync(1, 5)` executes:
        *   Fetches `AreaQueueState` for Area 1, gets `NextSequentialNumber` (e.g., 101).
        *   Fetches `CashierStation` 5, gets Name "Cassa 2".
        *   Updates `AreaQueueState` (Next=102, LastCalled=101, LastStationId=5, Timestamp=Now). Saves.
        *   Creates `CalledNumberBroadcastDto { AreaId: 1, TicketNumber: 101, CashierStationId: 5, CashierStationName: "Cassa 2", Timestamp: Now }`.
        *   Broadcasts DTO via SignalR to `Area-1` group.
        *   Returns `CalledNumberDto { TicketNumber: 101, CashierStationId: 5, CashierStationName: "Cassa 2" }` in API response.
    4.  Frontend (Cashier UI) receives API response, updates display: "Your Last Call: Ticket 101".
    5.  Frontend (Queue Display Screen) receives SignalR message, updates list `[ {Ticket=101, Station="Cassa 2"}, ... ]` and speaks "Number 101, please go to Cassa 2".
*   **4.2. Queue Display Page Load:** (Similar to previous version, relies on SignalR).
*   **4.3. Admin Resets Queue:** (Similar to previous version).

**5. User Stories** (Same as previous version, but implicitly include station assignment)
    *   5.1. **As a Customer, I want to see my ticket number called on a screen *and which cashier station to go to*, and hear it announced, so I know *where* to go.**
    *   5.2. **As a Cashier, I want to easily call the next ticket number in sequence *from my station*, so I can serve the next customer *at my counter*.**
    *   5.3. **As a Cashier, I want to be able to call a specific ticket number *to my station* if needed, so I can handle missed calls or out-of-sequence situations.**
    *   5.4. **As an Area Manager/Admin, I want to enable or disable the queue system for my area.**
    *   5.5. **As an Area Manager/Admin, I want to be able to reset the ticket sequence.**

**6. Future Considerations / Phase 2** (Same as previous version)
    *   Per-cashier station mini-displays.
    *   More sophisticated queue logic.
    *   Configuration for audio voice, speed, language.
    *   Visual themes for the display screen.
    *   Statistics on waiting times, numbers served.

---

This revised document now fully integrates the `CashierStation` into the workflow, ensuring customers are directed correctly. Let me know if you need any specific section expanded further.

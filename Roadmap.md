# SagraFacile - Combined Roadmap (Frontend & Backend)

This document outlines the planned development phases for the SagraFacile system, covering both the Frontend (Next.js) and Backend (.NET API) components.

## Working Process

*   **Task Management:** Development tasks are discussed and tracked primarily through session summaries captured in `sagrafacile-webapp/ProjectMemory.md`, `SagraFacile.NET/ProjectMemory.md`, and this `Roadmap.md`.
*   **Development Cycle:** We follow an iterative process:
    1.  **Plan:** Discuss requirements, review existing code/memory (also ApiRoutes.md and DataStructures.md), and outline steps (often in PLAN MODE).
    2.  **Implement:** Write code, create/modify files using available tools (in ACT MODE).
    3.  **Test:** User performs testing of implemented features.
    4.  **Refine:** Address bugs, incorporate feedback, and update documentation (`ProjectMemory.md`, `README.md`, `Roadmap.md`).
*   **Version Control:** Changes should be committed frequently with clear, descriptive messages (e.g., using `git commit`).

## Frontend (Next.js) Development Phases

### Phase 1: Core Functionality & Stabilization (Largely Complete)

*   **Features Implemented:**
    *   `[x]` Authentication & Authorization (JWT, Roles: SuperAdmin, OrgAdmin, Cashier)
    *   `[x]` Admin CRUD Operations (Organizations, Areas, Menu Categories, Menu Items, Users)
    *   `[x]` SuperAdmin Organization Context Switching
    *   `[x]` Cashier Interface (Area Selection, Menu Display, Cart Management, Order Submission, Payment Methods - Cash/POS, Basic Receipt Dialog/Print)
    *   `[x]` Public Pre-Order Interface (Dynamic Route, Menu Display, Cart, Order Submission, Email Confirmation w/ Backend QR Code)
    *   `[x]` Admin Orders History Page (View orders by Area, Totals)
*   **Current Focus (End of Phase 1):**
    *   `[ ]` **Comprehensive Testing:** Thoroughly test all implemented features across different roles and scenarios (Pre-Order flow, Admin pages, Cashier workflow). Address any bugs found. *(User Action Required)*
    *   `[ ]` **Backend Data Verification:** Confirm `OrderDto` structure from `GET /orders` includes necessary fields (`areaName`, `cashierName`, `orderDateTime`). *(Carried Over)*

### Phase 2: Real-time & Kitchen Integration

*   **Goal:** Enable real-time order updates and provide interfaces for waiters and kitchen staff.
*   **Key Tasks:**
    *   `[x]` **SignalR Client Setup:** Integrate `@microsoft/signalr` client library. Establish connection to the backend SignalR hub. *(Prerequisite for Waiter/KDS)*
    *   `[x]` **Waiter Interface Implementation (See `WaiterArchitecture.md`):**
        *   `[x]` **Routing & Access Control:** Create `/app/org/{orgId}/waiter` route, protect for "Waiter" role.
        *   `[x]` **Scanning Page:** Implement QR code scanning UI using a suitable library.
        *   `[x]` **Order Confirmation View:** Display fetched order details, add input for `TableNumber`.
        *   `[x]` **Confirmation Action:** Implement API call (`PUT /api/orders/{orderId}/confirm-preparation`) with `tableNumber`.
    *   `[x]` **Kitchen Display System (KDS) Implementation (See `KdsArchitecture.md`):**
        *   `[x]` **Admin UI:** Build UI under Area settings (`/app/org/{orgId}/admin/areas/{areaId}/kds`) for managing KDS Stations (CRUD) and assigning Menu Categories to them.
        *   `[x]` **KDS Interface Page:** Create the main KDS page (`/app/org/{orgId}/area/{areaId}/kds/{kdsId}`).
            *   `[x]` Implement SignalR connection for real-time updates.
            *   `[x]` Fetch and display active orders filtered for the station (`GET /api/orders/kds-station/{kdsStationId}`).
            *   `[x]` Implement the Order Detail modal/view triggered on order selection.
            *   `[x]` Implement the "tap-to-confirm" interaction for items within the detail view, calling the backend API (`PUT /api/orders/.../items/.../kds-status`).
            *   `[x]` Implement logic to update the main KDS list (remove completed orders, show partial status).
    *   `[ ]` **Real-time Updates (Other Interfaces):** Potentially update Admin/Cashier/Waiter views based on SignalR events (e.g., new orders, `Order.Status` changes triggered by KDS completion).
    *   `[ ]` **Order Status Flow:** Refine and implement the full lifecycle of `Order.Status` (including `Preparing`, `ReadyForPickup`) and the new `OrderItem.KdsStatus` across backend and frontend.

### Phase 2.5: Refinements & Enhancements (Post-Initial KDS/Waiter)

*   **Goal:** Address feedback from initial KDS/Waiter testing and improve core workflows.
*   **Key Tasks:**
    *   `[x]` **Enable HTTPS for Local Development:** Configure frontend dev server and backend API (Kestrel/Docker) for HTTPS to allow testing features requiring secure contexts (e.g., Waiter camera).
    *   `[x]` **Fix SuperAdmin Filtering:** Ensure KDS and Orders pages respect the selected organization context when logged in as SuperAdmin. (Backend `AreaService` updated, Frontend KDS page moved/refactored).
    *   `[x]` **Add KDS Admin Link:** Add a direct link to KDS Station management in the main Admin navigation sidebar (`/admin/kds`).
    *   `[x]` **Cashier Customer Name:** Make the Customer Name field mandatory in the Cashier UI.
    *   `[x]` **Refactor KDS UI/Workflow:**
        *   `[x]` Require an explicit confirmation button after marking all items. (`KdsOrderDetailDialog.tsx`)
        *   `[ ]` Prioritize Table Number and Customer Name display. *(Remaining UI task)*
        *   `[ ]` Add a view/button to see recently completed orders. *(Remaining UI task)*
    *   `[ ]` **Enhance Waiter UI:** Redesign Waiter UI with Tabs for 'Da Confermare' (Paid, PreOrder) and 'In Corso / Pronti' (Preparing, ReadyForPickup) lists, using a new mobile-friendly list component. Keep QR scan accessible.
    *   `[ ]` **Cashier Pre-Order Scan:** Add QR code scanning capability to quickly load pre-orders.
    *   `[ ]` **Simplify Roles:** Remove `OrgAdmin` role (requires backend changes and potentially updating existing user roles). *(Confirm Admin scope)*
    *   `[ ]` **Overhaul Admin Orders Page:** Implement SuperAdmin filtering and define/implement additional admin actions (e.g., status changes).

### Phase 3: Advanced Features

*   **Goal:** Improve usability, add advanced capabilities, and enhance robustness.
*   **Key Tasks:**
    *   `[ ]` **Implement Operational Day (Giornata/Day) Feature (Frontend Part):**
        *   `[ ]` **(Ready):** Define `Day` and update `OrderDto`, `KdsOrderDto` types in `src/types/index.ts` to include `dayId?`.
        *   `[ ]` **(Ready):** Update `apiClient.ts` with functions for the new `/api/days` endpoints.
        *   `[ ]` **(Ready):** Implement `DayContext` or integrate into `OrganizationContext` to fetch/store current Day (`GET /api/days/current`).
        *   `[ ]` **(Ready):** Add global UI indicator for current Day status (e.g., in the main layout).
        *   `[ ]` **(Ready):** Update Cashier UI (`/cashier/...`) to:
            *   Display Day status.
            *   Add "Apri Giornata" (`POST /api/days/open`) / "Chiudi Giornata" (`POST /api/days/{id}/close`) buttons (role-restricted).
            *   Disable order creation/payment confirmation if no Day is open (using context).
        *   `[ ]` **(Ready):** Update Waiter UI (`/waiter`) to:
            *   Display Day status.
            *   Potentially disable order confirmation if the order's `dayId` doesn't match the current open Day from context.
        *   `[ ]` **(Ready):** Update KDS UI (`/kds/...`) to:
            *   Display Day status.
            *   Rely on backend filtering (already implemented). Consider if history dialog needs Day context.
        *   `[ ]` **(Ready - Backend Pending):** Update Admin Orders UI (`/admin/orders`) to:
            *   Default to showing orders for the current Day.
            *   Add controls (e.g., Day selector using `GET /api/days`, date range?) to view historical orders (requires backend `GetOrdersAsync` update first).
        *   `[ ]` **(Ready):** Create Admin Days UI (New Page `/admin/days`) to view/manage Days (`GET /api/days`).
        *   `[ ]` **(Ready):** Handle Day-related errors/state across interfaces.
    *   `[ ]` **Admin/SuperAdmin Refinements:**
        *   `[ ]` Improved Default Redirection (e.g., redirect SuperAdmin to last used org).
        *   `[ ]` Smoother Org Switching Navigation (preserve sub-paths like `/admin/users` when switching orgs).
        *   `[ ]` Reporting/Analytics Dashboard.
    *   `[ ]` **General:**
        *   `[x]` Enhanced Error Handling: More specific error messages and recovery options.
        *   `[ ]` Improved Loading States: More granular loading indicators.
        *   `[ ]` Accessibility improvements.
    *   `[x]` **Implement Stock Management (Scorta) (See `docs/StockArchitecture.md`)**
        *   `[x]` **Goal:** Track item quantities, allow admin management, prevent selling out-of-stock items, and provide real-time visibility.
        *   `[x]` **Backend (.NET API):**
            *   `[x]` **Database:** Add `Scorta` (nullable int) to `MenuItem` model. Create and apply EF Core migration.
            *   `[x]` **DTOs:** Update `MenuItemDto`, `MenuItemUpsertDto` with `Scorta`. Create `StockUpdateBroadcastDto` for SignalR.
            *   `[x]` **Service Layer (`MenuItemService` or new `StockService`):**
                *   `[x]` Update `CreateMenuItemAsync`, `UpdateMenuItemAsync` to handle `Scorta`.
                *   `[x]` Implement `UpdateStockAsync(menuItemId, newScorta)`.
                *   `[x]` Implement `ResetStockAsync(menuItemId)`.
                *   `[x]` Implement `ResetAllStockForAreaAsync(areaId)`.
            *   `[x]` **API Controller (`MenuItemsController` or new `StockController`):**
                *   `[x]` Ensure existing `MenuItemsController` endpoints handle `Scorta`.
                *   `[x]` Add `PUT /api/menuitems/{menuItemId}/stock`.
                *   `[x]` Add `POST /api/menuitems/{menuItemId}/stock/reset`.
                *   `[x]` Add `POST /api/areas/{areaId}/stock/reset-all`.
            *   `[x]` **Order Processing (`OrderService.cs`):**
                *   `[x]` In `CreateOrderAsync` & `ConfirmPreOrderPaymentAsync`:
                    *   `[x]` Implement stock check before adding items. Throw exception if insufficient.
                    *   `[x]` Implement transactional stock decrement after validation, before saving order.
                *   `[x]` Broadcast `StockUpdateBroadcastDto` via SignalR after stock decrement.
            *   `[x]` **SignalR (`OrderHub.cs`):** Ensure `Area-{areaId}` group exists for stock updates.
        *   `[x]` **Frontend (Next.js App):**
            *   `[x]` **Types (`src/types/index.ts`):** Add `scorta` to `MenuItemDto`. Create `StockUpdateBroadcastDto`.
            *   `[x]` **Admin UI:**
                *   `[x]` Add `Scorta` input to menu item create/edit forms. Display `Scorta` in list.
                *   `[x]` Add "Reset Stock" button for individual items.
                *   `[ ]` (Optional) New Admin UI section for `ResetAllStockForAreaAsync` and quick stock updates.
            *   `[x]` **Cashier Interface (`CashierPage.tsx`, `CashierMenuPanel.tsx`):**
                *   `[x]` Display `scorta` (e.g., "Scorta: 5", "Illimitata", "Esaurito") with clear visuals.
                *   `[x]` Client-side pre-check in `handleAddItem` to warn/prevent adding if stock is low (backend is final authority).
                *   `[x]` Visually indicate out-of-stock items.
                *   `[x]` Listen for `"ReceiveStockUpdate"` SignalR message and update local `menuItems` state.
            *   `[x]` **API Client (`apiClient.ts` or `stockService.ts`):** Add functions for new stock management endpoints.
        *   `[ ]` **UX for Pre-orders with Unavailable Items:**
            *   `[ ]` No stock decrement at initial pre-order.
            *   `[ ]` Stock check at pre-order confirmation by cashier.
            *   `[ ]` If unavailable, backend throws error, frontend displays error, cashier modifies order.
    *   `[ ]` **Handle Imported Pre-orders:**
        *   `[ ]` Update Cashier UI to display orders with `PreOrder` status fetched by the background service.
        *   `[ ]` Ensure the flow for confirming/paying these imported pre-orders works correctly (potentially using the QR scan feature).
    *   `[x]` **Add Order Details (Coperti & Asporto):**
        *   `[x]` **(Frontend Part):**
            *   `[x]` Update `OrderDto` type in `src/types/index.ts` to include `numberOfGuests` (Coperti) and `isTakeaway` (Asporto).
            *   `[x]` **Cashier UI:** Add numeric input for `numberOfGuests` and a checkbox for `isTakeaway` (default false).
            *   `[x]` **Pre-order Page:** Add numeric input for `numberOfGuests` and a checkbox for `isTakeaway` (default false).
            *   `[x]` Ensure `numberOfGuests` and `isTakeaway` values are sent to the backend when creating/submitting orders.
            *   `[x]` Display `numberOfGuests` and `isTakeaway` in relevant order detail views (e.g., Admin Orders, potentially Waiter/KDS if applicable).
    *   `[x]` **Receipt Enhancements:**
        *   `[x]` Group order items by menu category in cart display and receipts.
        *   `[x]` Prominently display "ASPORTO" status for takeaway orders in receipts.
        *   `[x]` Add "Clear Order" button to Cashier interface.
        *   `[x]` Improve receipt clarity with better formatting and information organization.
        *   `[x]` Fix reprint functionality to handle missing menu data gracefully.

    *   `[~]` **Implement Enhanced Order Identification (Human-Readable Order Numbers) (See `docs/DisplayOrderNumberArchitecture.md`)**
        *   `[x]` **Goal:** Introduce a human-readable, day/area-unique order number (e.g., `CUC-001`) for improved operational clarity, replacing the internal `Order.Id` for display purposes.
        *   `[x]` **Backend (.NET API):**
            *   `[x]` Add `DisplayOrderNumber` (string) to `Order` model.
            *   `[x]` Create `AreaDayOrderSequence` entity (`AreaId`, `DayId`, `LastSequenceNumber`) to track daily sequences per area.
            *   `[x]` Modify `OrderService` to generate `Order.Id` as `Guid.NewGuid().ToString()` for new orders.
            *   `[x]` Implement logic in `OrderService` (`CreateOrderAsync`, `ConfirmPreOrderPaymentAsync`) to:
                *   `[x]` Derive a 3-char uppercase alphanumeric prefix from `Area.Slug`.
                *   `[x]` Increment `LastSequenceNumber` in `AreaDayOrderSequence`.
                *   `[x]` Construct and save `DisplayOrderNumber` (e.g., `SLG-001`).
            *   `[x]` Update `PrinterService` to use `DisplayOrderNumber`, Date, and Area Name/Slug on printouts.
            *   `[x]` Add `DisplayOrderNumber` to `OrderDto`, `KdsOrderDto`, `OrderStatusBroadcastDto`, etc. (Backend DTOs).
            *   `[x]` Create and apply EF Core migration for database changes.
        *   `[~]` **Frontend (Next.js App):**
            *   `[x]` Update `OrderDto`, `KdsOrderDto`, `OrderStatusBroadcastDto` types in `src/types/index.ts`.
            *   `[~]` Update UI components (Cashier, KDS, Public Displays, Admin Orders, Receipt Dialogs) to display `order.displayOrderNumber` along with Date and Area Name/Slug. (ReceiptDialog.tsx updated)
            *   `[ ]` Add an informational note in Area Admin UI regarding `Area.Slug`'s influence on the order number prefix.
        *   `[x]` **Documentation:**
            *   `[x]` Create `docs/DisplayOrderNumberArchitecture.md`. (Already done)

### Phase 4: Workflow, Advanced Printing

*   **Key Tasks:**

    *   **Implement Configurable Order Workflow (See `WorkflowArchitecture.md`)**
        *   `[x]` **Backend:**
            *   `[x]` Add `EnableWaiterConfirmation`, `EnableKds`, `EnableCompletionConfirmation` flags to `Area` model & create migration.
            *   `[x]` Refactor `OrderService` methods (`CreateOrderAsync`, `ConfirmPreOrderPaymentAsync`, `ConfirmOrderPreparationAsync`, `ConfirmKdsOrderCompletionAsync`) to implement state transition logic based on Area flags.
            *   `[ ]` If `EnableCompletionConfirmation` is needed: Define trigger, implement `ConfirmOrderPickupAsync` service method and API endpoint.
        *   `[x]` **Frontend:**
            *   `[x]` Add toggles for workflow flags to Area Admin UI (`/admin/areas/{areaId}`).
            *   `[ ]` If `EnableCompletionConfirmation` is needed: Implement UI trigger for pickup confirmation.

    *   **Implement Advanced Printing Architecture (See `PrinterArchitecture.md`)**
        *   `[ ]` **Backend - Schema & Config (Phase 1 - Mostly Complete):**
            *   `[x]` Define `Printer` and `PrinterCategoryAssignment` models. Update `Area` model (`ReceiptPrinterId`, `PrintComandasAtCashier`). Create migration.
            *   `[x]` Create `PrintersController` for Admin CRUD API (`/api/printers`). Update `AreasController` for new Area fields.
            *   `[x]` Create `PrinterAssignmentsController` for managing category-to-printer mappings (`GET`, `POST /api/printers/{printerId}/assignments`).
            *   `[x]` **Define `CashierStation` Model and Integration:**
                *   `[x]` Define `CashierStation` entity (`Id`, `OrganizationId`, `AreaId`, `Name`, `ReceiptPrinterId`, `PrintComandasAtThisStation`, `IsEnabled`).
                *   `[x]` Add `CashierStationId` (nullable FK) to `Order` model. Create migration (`AddCashierStationAndLinkToOrder`).
                *   `[x]` Create `CashierStationsController` for CRUD API (`/api/cashierstations/...`). (DTOs, Service Interface & Implementation, Controller created)
        *   `[x]` **Backend - Core Print Service & Integration (Phase 2 - Mostly Complete):**
            *   `[x]` **`OrderService` Update:** Accept and store `CashierStationId` when creating/updating orders (**Update:** Now also handles `CashierStationId` in `ConfirmPreOrderPaymentAsync`).
            *   `[x]` **`PrintService` Implementation (`IPrintService`):**
                *   `[x]` Add ESC/POS generation logic (including QR codes, **item notes**) using an `EscPosDocumentBuilder`.
                *   `[x]` Implement network printing via TCP socket (`SendToPrinterAsync` for `Network` type).
                *   `[x]` Implement SignalR job dispatch logic (`SendToPrinterAsync` for `WindowsUsb` type, using Hub registry).
                *   `[x]` Determine target printer(s) based on `Order.CashierStationId` / `Area` defaults.
                *   `[x]` **(NEW):** Implement reprint logic (`ReprintOrderDocumentsAsync`) directing output to station/area default printer and consolidating comandas.
            *   `[x]` **`OrderHub` Enhancements:** Add `_printerConnections` registry and `RegisterPrinterClient` method.
            *   `[x]` **`OrderService` Integration (Mostly Complete):**
                *   `[x]` Inject `IPrintService` into `OrderService`.
                *   `[x]` Call `_printService.PrintOrderDocumentsAsync` from `OrderService` at appropriate points.
                *   `[x]` **(NEW):** Added `POST /api/orders/{orderId}/reprint` endpoint calling `_printerService.ReprintOrderDocumentsAsync`.
        *   `[x]` **Frontend - Admin UI (Complete for this phase):**
            *   `[x]` Build Printer Management page (`/admin/printers`).
            *   `[x]` Build Category Assignment page (`/admin/printer-assignments`).
            *   `[x]` Update Area Management page (`/admin/areas/{areaId}`) with `ReceiptPrinterId` dropdown and `PrintComandasAtCashier` toggle.
            *   `[x]` Build Cashier Station Management page (`/admin/cashier-stations`).
        *   `[x]` **Frontend - Cashier Interface (Complete for this phase):**
            *   `[x]` **Station Selection UI:**
                *   `[x]` Implemented station selection/persistence/display.
            *   `[x]` **Order Creation:** Include `CashierStationId` in payload.
            *   `[x]` **Pre-Order Confirmation:** Include `CashierStationId` in payload.
            *   `[x]` **Receipt/Reprint Dialogs:**
                *   `[x]` Removed old client-side WebSocket printing logic.
                *   `[x]` Implemented reprint choice UI calling new backend reprint endpoint.
            *   `[x]` **Admin Orders Page Reprint:** Implemented reprint functionality on the Admin Orders page with printer selection.
        *   `[ ]` **Windows Companion App (`SagraFacile.WindowsPrinterService`) (NEXT MAJOR COMPONENT):**
            *   `[x]` Implemented core SignalR connection, registration, message handling, and raw printing.
            *   `[ ]` **Outstanding User Actions:** `.csproj` updates, `SettingsForm` UI designer changes.
            *   `[ ]` **Backend Verification:** Ensure `PrintService.cs` sends correct arguments (`jobId`, `windowsPrinterName`, `escPosData`) to the hub method.
        *   `[ ]` **Testing (End-to-End) (NEXT FOCUS):**
            *   `[ ]` Test CashierStation management UI and API.
            *   `[ ]` Test Cashier Station selection in Cashier UI and persistence with orders (creation & pre-order confirmation).
            *   `[ ]` Test printing receipts and comandas (including notes) with various configurations:
                *   Network printers.
                *   USB printers via Companion App.
                *   Area default printer.
                *   Cashier Station specific printer.
                *   `PrintComandasAtThisStation` vs. `PrintComandasAtCashier`.
                *   Category-based comanda printing.
            *   `[x]` Test reprint functionality from Cashier (Receipt Only, Receipt+Comandas) and verify target printer/comanda consolidation.
            *   `[ ]` Test reprint functionality from Admin Orders page (including printer selection) and verify target printer/comanda consolidation.
    *   `[x]` **Implement On-Demand Printing for Windows Companion App (See `docs/PrinterArchitecture.md#6-on-demand-printing-for-windows-companion-app`)**
        *   `[x]` **Backend - API Changes:**
            *   `[x]` Add `PrintMode` enum (`Immediate`, `OnDemandWindows`) and property to `Printer` entity.
            *   `[~]` Create/apply migration. (Migration created, application deferred by user)
            *   `[x]` Update `PrinterDto`, `PrinterUpsertDto`, `PrinterService` to handle `PrintMode`.
            *   `[x]` Create `GET /api/printers/config/{instanceGuid}` endpoint to return `PrintMode` and `WindowsPrinterName`.
        *   `[x]` **Frontend - Admin UI:**
            *   `[x]` Add `PrintMode` selection to printer configuration form (`sagrafacile-webapp/src/components/admin/PrinterFormDialog.tsx`).
        *   `[x]` **Windows Companion App (`SagraFacile.WindowsPrinterService`):**
            *   `[x]` **`SignalRService.cs`:**
                *   `[x]` Fetch `PrintMode` from backend on registration.
                *   `[x]` Implement in-memory `ConcurrentQueue<PrintJobItem>`.
                *   `[x]` Handle incoming jobs: queue if `OnDemandWindows`, print if `Immediate`.
                *   `[x]` Expose methods to dequeue jobs and get queue count. Raise `OnDemandQueueCountChanged` event.
            *   `[x]` **`PrintStationForm.cs` (New Form):**
                *   `[x]` UI: Pending count label, "Print Next" button, activity log, connection status. Anchored for responsiveness.
                *   `[x]` Logic: Get initial count, subscribe to queue changes, dequeue/print job via `SignalRService` and `IRawPrinter`.
            *   `[x]` **`ApplicationLifetimeService.cs`:**
                *   `[x]` Launch `PrintStationForm` as main window.
                *   `[x]` Manage `PrintStationForm` instance (including minimize-to-tray and show/hide from tray).
            *   `[ ]` **(Optional Later) `SettingsForm.cs`:** Add setting for "Number of comandas to print per click".

### Phase 5: Customer Queue System (See `docs/CustomerQueueArchitecture.md`)

*   **Goal:** Implement a queue management system for customers at cashier stations.
*   **Key Tasks (Backend - .NET API):**
    *   `[x]` **Database Changes:**
        *   `[x]` Implement `AreaQueueState` entity.
        *   `[x]` Add `EnableQueueSystem` to `Area` entity.
        *   `[x]` Create and apply EF Core migration. (User applied)
    *   `[x]` **New Service: `IQueueService` / `QueueService.cs`:** (All sub-tasks marked as done)
    *   `[x]` **API Endpoints (New Controller: `QueueController.cs`):** (All sub-tasks marked as done)
    *   `[x]` **SignalR (e.g., `QueueHub.cs` or extend `OrderHub.cs`):**
        *   `[x]` Implement `QueueNumberCalled` message broadcast.
        *   `[x]` Implement `QueueReset` message broadcast.
        *   `[x]` Implement `QueueStateUpdated` message broadcast.
        *   `[x]` Implemented group joining/leaving methods (`JoinAreaQueueGroup`, `LeaveAreaQueueGroup`) in `OrderHub.cs`.
    *   `[x]` **DTOs:** (All relevant DTOs created and aligned with frontend)
*   **Key Tasks (Frontend - Next.js App):**
    *   `[x]` **Cashier Interface (`/app/app/org/[orgId]/cashier/area/[areaId]/page.tsx`):**
        *   `[x]` UI Elements for queue display and controls.
        *   `[x]` Logic for fetching state and calling numbers.
        *   `[x]` Integrate SignalR for real-time updates (including group joining and event handling).
    *   `[x]` **New Queue Display Page (e.g., `/qdisplay/org/{orgSlug}/area/{areaId}`):**
        *   `[x]` UI for displaying called numbers.
        *   `[x]` SignalR integration (group joining, event handling) and audio playback for announcements.
    *   `[x]` **Admin Interface (`/app/app/org/[orgId]/admin/areas/page.tsx`):**
        *   `[x]` Add `Switch` for `EnableQueueSystem` in Area settings.
    *   `[ ]` **(Optional) New Admin Queue Management Page:**
        *   `[ ]` UI for displaying queue state, reset, and set next number.
    *   `[x]` **New Services/Hooks:**
        *   `[x]` `queueService.ts` for API calls.
        *   `[x]` `useSignalRHub` hook utilized effectively for SignalR connections.
    *   `[x]` **Types (`types/index.ts`):**
        *   `[x]` Add DTOs: `QueueStateDto`, `CalledNumberDto`, `CalledNumberBroadcastDto`, `QueueStateUpdateBroadcastDto`, `QueueResetBroadcastDto`.
        *   `[x]` Update `AreaDto` with `enableQueueSystem`.

### Phase 5.5: Public Order Pickup Display (See `docs/OrderPickupDisplayArchitecture.md`)

*   **Goal:** Implement a public-facing screen to display orders ready for pickup, with real-time updates and audio announcements.
*   **Key Tasks (Backend - .NET API):**
    *   `[x]` **DTO:** Create `OrderStatusBroadcastDto.cs` for SignalR payloads.
    *   `[x]` **`OrderService.cs` Refactor:**
        *   `[x]` Modify `SendOrderStatusUpdateAsync` to broadcast `OrderStatusBroadcastDto` via a new SignalR message (e.g., `"ReceiveOrderStatusUpdate"`).
        *   `[x]` Implement `GetOrdersByStatusAsync(int areaId, OrderStatus status)` to fetch orders for the display's initial load (filtered by current open day). *(Currently under investigation for returning empty results unexpectedly)*
        *   `[x]` Ensure `ConfirmOrderPickupAsync` correctly transitions status to `Completed` and triggers the updated `SendOrderStatusUpdateAsync`.
    *   `[x]` **`PublicController.cs`:** Add new public endpoint `GET /api/public/areas/{areaId}/orders/ready-for-pickup` calling `_orderService.GetOrdersByStatusAsync`.
    *   `[x]` **`OrdersController.cs`:** Ensure `PUT /api/orders/{orderId}/confirm-pickup` endpoint correctly calls `_orderService.ConfirmOrderPickupAsync` and is appropriately authorized.
    *   `[x]` **`OrderHub.cs`:** Verify group joining (`JoinAreaQueueGroup`) is suitable for public display clients.
*   **Key Tasks (Frontend - Next.js App):**
    *   `[x]` **New Public Display Page (`/pickup-display/org/{orgSlug}/area/{areaId}/page.tsx`):**
        *   `[x]` Fetch initial `ReadyForPickup` orders via the new public API.
        *   `[x]` Connect to `OrderHub` (`useSignalRHub`), join `Area-{areaId}` group.
        *   `[x]` Listen for `"ReceiveOrderStatusUpdate"`: update list, trigger TTS.
        *   `[x]` Implement UI for displaying orders and TTS announcements (consider reusing/refactoring TTS from `/qdisplay`).
    *   `[x]` **New Staff Pickup Confirmation Page (`/app/org/{orgId}/area/{areaId}/pickup-confirmation/page.tsx`):**
        *   `[x]` Fetch `ReadyForPickup` orders for the area.
        *   `[x]` Implement UI with "Confirm Pickup" button per order.
        *   `[x]` Button calls `orderService.confirmOrderPickup(orderId)`.
        *   `[x]` Role-protect this page.
    *   `[x]` **Types & Services:**
        *   `[x]` Add `OrderStatusBroadcastDto` to `types/index.ts`.
        *   `[x]` Add new functions to `orderService.ts` (or `apiClient.ts`) for fetching public ready orders and confirming pickup.
*   **Workflow Configuration:**
    *   `[ ]` Remind admin to set `EnableCompletionConfirmation = true` for Areas using this display.
    *   `[ ]` **Debugging:** Investigate and resolve issue with `OrderService.GetOrdersByStatusAsync` not returning expected orders for the public display (potentially `DayId` filtering).

### Phase 5.6: Queue Display Advertising Carousel (Completed)

*   **Goal:** Enhance the public queue display with a rotating carousel of advertisements (images/videos) to utilize screen real estate for promotional content.
*   **Key Tasks (Frontend):**
    *   `[x]` **Create `AdCarousel.tsx` Component:** Built a reusable component to manage and display a list of media items with rotation logic for images (timed) and videos (on-ended).
    *   `[x]` **Update `QueueDisplayPage`:** Restructured the page layout to include a dedicated section for the `AdCarousel` component and connected it to a dynamic backend endpoint.
    *   `[x]` **Phase 1 Implementation:** Integrated the carousel with a hardcoded list of media for initial deployment.
*   **Key Tasks (Backend & Admin UI - Phase 2 of feature):**
    *   `[x]` **Database:** Added `AdMediaItems` table to store ad metadata and file paths.
    *   `[x]` **Storage:** Configured backend to store and serve media files from a persistent local directory (`wwwroot/media/promo/`) to avoid ad-blockers.
    *   `[x]` **API:** Created public and admin endpoints to manage and retrieve ad media.
    *   `[x]` **Admin UI:** Built a new interface for uploading and managing ad content per Area, including automatic video duration detection.
*   **Documentation:**
    *   `[x]` Create `docs/AdCarouselArchitecture.md`.
*   **Debugging & Finalization:**
    *   `[x]` Resolved media serving issues by enabling static files on the backend, correcting URL construction on the frontend, and whitelisting the image host in `next.config.js`.

### Phase 6: Mobile Table Ordering & Payment Interface

*   **Goal:** Develop a mobile-first interface for waiters/admins to take orders, including table numbers, and process payments directly at the table, streamlining the workflow.
*   **Backend Tasks (.NET API):**
    *   `[ ]` **`OrderService.CreateOrderAsync` Enhancements:**
        *   `[ ]` Ensure `CreateOrderDto` fields (`TableNumber`, `PaymentMethod`, `AmountPaid`) are fully processed.
        *   `[ ]` Set `Order.CashierId` to the ID of the authenticated user taking the order.
        *   `[ ]` Implement status transition logic: If `TableNumber` is present and `Area.EnableWaiterConfirmation` is true, set `Order.Status` directly to `Preparing` (or subsequent states based on `EnableKds`/`EnableCompletionConfirmation`), effectively combining order creation with waiter confirmation. `Order.WaiterId` should also be set to the authenticated user's ID in this scenario.
        *   `[ ]` Verify comanda and receipt printing logic aligns with the order's initial effective status determined by this new flow.
*   **Frontend Tasks (Next.js App):**
    *   `[ ]` **New Route & Page:** Create `/app/org/[orgId]/table-order/area/[areaId]/`.
    *   `[ ]` **UI Development (MobileTableOrderPage):**
        *   `[ ]` Adapt components and logic from `pre-order-menu.tsx` (menu display, cart) and `CashierPage.tsx` (payment, receipt dialog).
        *   `[ ]` Integrate `AuthContext`, `OrganizationContext`, and fetch `AreaDto` via URL params.
        *   `[ ]` Implement order details form including `customerName`, `isTakeaway`, `numberOfGuests`, and a mandatory `tableNumber` input.
        *   `[ ]` Implement payment section with "Contanti" (including amount tendered input) and "POS" buttons.
        *   `[ ]` Ensure `CreateOrderDto` is correctly populated with all details (including payment info and table number) and sent to `POST /api/orders`.
        *   `[ ]` Integrate `ReceiptDialog` for post-payment summary and print triggering.
    *   `[ ]` **Real-time:** Implement SignalR listener for `ReceiveStockUpdate` to keep menu stock levels current.
    *   `[ ]` **Security:** Protect the new route for "Waiter" and "Admin" roles.
*   **Documentation Tasks:**
    *   `[x]` Create `docs/MobileTableOrderingArchitecture.md` detailing the design and workflow of this feature. (Already completed in previous step)

### Phase 7: Deployment & Monitoring

*   **Goal:** Prepare the application for production use and simplify deployment for end-users.
*   **Key Tasks:**
    *   `[ ]` Containerization (Dockerfile for frontend).
    *   `[ ]` Deployment Strategy (Cloud vs. On-premise, CI/CD pipeline).
    *   `[ ]` Production Configuration (Environment variables, security hardening).
    *   `[ ]` Monitoring & Logging Setup (Error tracking, performance monitoring).
    *   `[ ]` **Simplified Deployment Package & Process:**
        *   `[ ]` **Interactive Setup Scripts (`start.sh`, `start.bat`):**
            *   `[ ]` Implement logic to check for `sagrafacile_config.json`.
            *   `[ ]` Add interactive prompts for `MY_DOMAIN`, `CLOUDFLARE_API_TOKEN`, database credentials, `JWT_SECRET`.
            *   `[ ]` Add prompt for demo data seeding (`SAGRAFACILE_SEED_DEMO_DATA`) or initial admin/org setup (`INITIAL_ADMIN_EMAIL`, `INITIAL_ADMIN_PASSWORD`, `INITIAL_ORGANIZATION_NAME`).
            *   `[ ]` Save user choices to `sagrafacile_config.json`.
            *   `[ ]` Generate `.env` file from `sagrafacile_config.json`.
        *   `[ ]` **API Backend (`Program.cs`) Adjustments:**
            *   `[ ]` Implement logic to check `SAGRAFACILE_SEED_DEMO_DATA`.
            *   `[ ]` If `false`, use `INITIAL_ADMIN_EMAIL`, `INITIAL_ADMIN_PASSWORD`, `INITIAL_ORGANIZATION_NAME` to create initial setup.
        *   `[ ]` **Deployment ZIP Package:**
            *   `[ ]` Define contents for the distributable ZIP (excluding source code, including scripts, `docker-compose.yml`, `Caddyfile`, `docs/`, `sagrafacile_config.json.example`, printer service installer).
        *   `[ ]` **Automated GitHub Release Packaging:**
            *   `[ ]` Create GitHub Actions workflow (`.github/workflows/release-zip.yml`) to build the ZIP and create a release on new version tags.
        *   `[ ]` **Documentation Updates:**
            *   `[ ]` Update `README.md` installation instructions for the new interactive scripts and Cloudflare Let's Encrypt setup.
            *   `[ ]` Update `DEPLOYMENT_ARCHITECTURE.md` to reflect interactive scripts, `sagrafacile_config.json`, API changes for seeding, and ZIP package contents.
            *   `[ ]` Ensure service name consistency (`api` for backend) across `docker-compose.yml`, `Caddyfile`, and docs.

### Phase 8: Guest & Takeaway Charges (Completed)

*   **Goal:** Implement a configurable system to add charges for guests (coperto) and for takeaway orders (asporto).
*   **Backend Tasks (.NET API):**
    *   `[x]` **Database Model:** Add `GuestCharge` and `TakeawayCharge` (decimal) to the `Area` entity. Create and apply EF Core migration.
    *   `[x]` **DTOs:** Update `AreaDto` and `AreaUpsertDto` to include the new charge fields.
    *   `[x]` **`OrderService`:** Modify `CreateOrderAsync` and `ConfirmPreOrderPaymentAsync` to calculate and add these charges to the `Order.TotalAmount` based on `NumberOfGuests` and `IsTakeaway` flags.
    *   `[x]` **`PrinterService`:** Update receipt generation logic to display these charges as separate line items for clarity.
*   **Frontend Tasks (Next.js App):**
    *   `[x]` **Admin UI:** Add input fields for "Guest Charge" and "Takeaway Charge" to the Area management form.
    *   `[x]` **Receipt Dialog:** Update the `ReceiptDialog` component to display the new charges, mirroring the final printed receipt.

### Phase 9: Charts & Analytics Dashboard (See `docs/ChartsAnalyticsArchitecture.md`)

*   **Goal:** Implement comprehensive analytics and charts for the admin interface to provide insights into sales, orders, and operational metrics.
*   **Key Features:**
    *   **Dashboard KPIs:** Mobile-friendly key performance indicators (sales, order count, average order value, top category)
    *   **Dashboard Charts:** Desktop/tablet-only charts (sales trend, order status distribution, top menu items)
    *   **Orders Analytics:** Detailed charts on orders admin page (orders by hour, payment methods, AOV trend, status timeline)
    *   **Day-Based Analysis:** Leverage operational day (Giornata) system for accurate reporting
    *   **Responsive Design:** KPIs on all devices, charts hidden on mobile
    *   **Export Reports:** Generate PDF/Excel reports for administrative purposes
*   **Backend Tasks (.NET API):**
    *   `[x]` **New Controller:** Create `AnalyticsController` with endpoints for dashboard and orders analytics (`SagraFacile.NET.API/Controllers/AnalyticsController.cs`).
    *   `[x]` **DTOs:** Create analytics DTOs (`DashboardKPIsDto`, `SalesTrendDataDto`, `OrderStatusDistributionDto`, `OrderStatusTimelineEventDto`, etc.) in `SagraFacile.NET.API/DTOs/Analytics/`.
    *   `[x]` **Service Layer:** Define `IAnalyticsService` interface and implement `AnalyticsService.cs` with all data querying and business logic. Register in `Program.cs`.
    *   `[x]` **Database Queries:** Implemented efficient queries leveraging the `Day` table for accurate day-based reporting (within `AnalyticsService.cs`).
    *   `[x]` **Report Generation:** Implemented text-based report generation for daily summaries and area performance reports (within `AnalyticsService.cs`). (PDF/Excel export is a future enhancement).
*   **Frontend Tasks (Next.js App):**
    *   `[ ]` **Setup:** Install shadcn charts component (`npx shadcn-ui@latest add charts`)
    *   `[ ]` **Component Structure:** Create organized chart components in `src/components/charts/`
        *   `[ ]` Dashboard components (`DashboardKPIs`, `SalesTrendChart`, `OrderStatusChart`, `TopMenuItemsChart`)
        *   `[ ]` Orders components (`OrdersByHourChart`, `PaymentMethodsChart`, `AverageOrderValueChart`, `OrderStatusTimelineChart`)
        *   `[ ]` Shared components (`ChartContainer`, `LoadingChart`, `EmptyChart`, `ChartErrorBoundary`)
    *   `[ ]` **Dashboard Integration:** Add KPIs and responsive charts to admin home page (`/admin/page.tsx`)
    *   `[ ]` **Orders Page Integration:** Add analytics section to orders admin page (`/admin/orders/page.tsx`)
    *   `[ ]` **Services:** Create `analyticsService.ts` for API calls with caching and error handling
    *   `[ ]` **Responsive Behavior:** Implement `useMediaQuery` hook for chart visibility control
    *   `[ ]` **Types:** Add analytics DTOs to `src/types/index.ts`
*   **Technical Specifications:**
    *   **Refresh Strategy:** Periodic API calls (5-minute intervals, configurable)
    *   **Date Ranges:** Default 7 days with custom range capability
    *   **Performance:** In-memory caching with 5-minute TTL
    *   **Error Handling:** Comprehensive error boundaries and loading states
    *   **Security:** Same role-based access as existing admin pages
*   **Implementation Phases:**
    *   `[x]` **Phase 9.1 (Backend):** Foundation setup (DTOs, Service Interface, Controller, DI Registration).
    *   `[x]` **Phase 9.2 (Backend):** Full implementation of data querying and business logic in `AnalyticsService.cs` for all defined analytics endpoints and text-based report generation.
    *   `[x]` **Phase 9.3 (Frontend):** Foundation setup (charts component installation, `analyticsService.ts`, TypeScript types).
    *   `[x]` **Phase 9.4 (Frontend):** Dashboard KPIs and charts component implementation and integration.
    *   `[ ]` **Phase 9.5 (Frontend):** Orders analytics charts component implementation and integration.
    *   `[ ]` **Phase 9.6 (Frontend & Backend):** Polish, comprehensive testing, potential enhancement of report export (e.g., PDF/Excel), and user feedback integration.

*(This roadmap is a living document and will be updated as the project progresses.)*

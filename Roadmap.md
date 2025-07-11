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
    *   `[x]` **Support for Standard Printers & Customizable Templates (NEW)**
        *   `[x]` **Goal:** Support non-ESC/POS printers (laser, inkjet) via PDF generation and allow template customization for both PDF and ESC/POS outputs.
        *   `[x]` **Backend (.NET API):**
            *   `[x]` **Database:**
                *   `[x]` Add `DocumentType` (enum: `EscPos`, `HtmlPdf`) to `Printer` entity.
                *   `[x]` Create new `PrintTemplate` entity with fields for `DocumentType`, `HtmlContent`, `EscPosHeader`, `EscPosFooter`.
                *   `[x]` Create and apply EF Core migration.
            *   `[x]` **New `PdfService`:** Implement service using Puppeteer Sharp to convert HTML to PDF.
            *   `[x]` **New Templating Engine:** Integrate Scriban to process HTML templates.
            *   `[x]` **Refactor `PrinterService`:**
                *   `[x]` Modify logic to check `Printer.DocumentType`.
                *   `[x]` If `HtmlPdf`, use `PdfService` to generate PDF content for the `PrintJob`.
                *   `[x]` If `EscPos`, use `EscPosDocumentBuilder` and apply header/footer from `PrintTemplate`.
        *   `[x]` **Windows Companion App:**
            *   `[x]` Enhance SignalR message to include `contentType` (`application/pdf` or `application/vnd.escpos`).
            *   `[x]` Add logic to handle PDF jobs by saving to a temp file and printing via Windows Shell API.
        *   `[x]` **Frontend (Admin UI):**
            *   `[x]` Add "Document Type" dropdown to Printer configuration form.
            *   `[x]` Create new Admin page (`/admin/print-templates`) for managing templates with conditional UI for HTML vs. ESC/POS fields.
            *   `[x]` Implement "Restore Defaults" button and "Preview" functionality for templates.
    *   `[x]` **Implement Resilient Printing via Job Queue (NEW - TOP PRIORITY)**
        *   `[x]` **Goal:** Rearchitect the printing system to be asynchronous and fault-tolerant, ensuring no print jobs are lost.
        *   `[x]` **Backend (.NET API):**
            *   `[x]` **Database:** Create `PrintJob` entity and EF Core migration.
            *   `[x]` **New Service (`PrintJobProcessor`):** Implement a `BackgroundService` to poll for pending jobs.
            *   `[x]` **"Fast Lane" Signaling:** Implement an in-memory signaling mechanism to trigger the `PrintJobProcessor` instantly for high-priority jobs (e.g., receipts).
            *   `[x]` **Refactor `PrinterService`:** Change `PrintOrderDocumentsAsync` to create and save `PrintJob` entities to the database instead of printing directly. Move `SendToPrinterAsync` logic to the `PrintJobProcessor`.
            *   `[x]` **Refactor `OrderService`:** Ensure it calls the new `PrinterService` methods correctly and handles the fast, asynchronous response.
        *   `[x]` **Windows Companion App:**
            *   `[x]` Update the app to receive a `PrintJobId` with each job.
            *   `[x]` Implement a callback (`ReportPrintJobStatus`) via SignalR to inform the backend of print success or failure.
        *   `[x]` **Admin UI (Phase 2 of this feature):**
            *   `[x]` Create a new page (`/admin/print-jobs`) to monitor job statuses, view errors, and manually trigger retries.
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
    *   `[ ]` **Enhanced Observability & Logging Setup:**
        *   `[ ]` **Implement Comprehensive Backend Logging with Serilog (See `docs/LoggingStrategy.md`)**
            *   **Status:** Planned
            *   **Description:** Integrate Serilog into the .NET API for structured, configurable, and extensible logging. This will improve debugging capabilities, allow for better performance monitoring, and provide insights into application behavior during production.
            *   **Key Sub-Tasks:**
                *   Install and configure Serilog with console output (JSON formatted for easier parsing).
                *   Implement request/response logging middleware (e.g., `UseSerilogRequestLogging()`).
                *   Add contextual logging (with structured properties) to critical services (e.g., `OrderService`, `PrinterService`, `AnalyticsService`) and controllers.
                *   Establish conventions for log levels and ensure important business events and errors are logged effectively.
                *   Create `docs/LoggingStrategy.md` detailing the logging approach, configuration, and best practices.
            *   **Benefit:** Improved troubleshooting, performance analysis, and system stability, laying groundwork for future log management solutions.
        *   `[ ]` **Frontend Error Tracking:** Implement a client-side error tracking solution (e.g., Sentry free tier) for the Next.js application to capture and report JavaScript errors.
        *   `[ ]` **Basic Infrastructure Monitoring:** Document procedures for using `docker stats` and host OS tools to monitor container and system resource usage.
    *   `[x]` **Simplified Deployment Package & Process:**
        *   `[x]` **Interactive Setup Scripts (`start.sh`, `start.bat`):**
            *   `[x]` (`start.sh`) Implement logic to check for `sagrafacile_config.json`.
            *   `[x]` (`start.sh`) Add interactive prompts for `MY_DOMAIN`, `CLOUDFLARE_API_TOKEN`, database credentials, `JWT_SECRET`.
            *   `[x]` (`start.sh`) Add prompt for demo data seeding (`SAGRAFACILE_SEED_DEMO_DATA`) or initial admin/org setup (`INITIAL_ADMIN_EMAIL`, `INITIAL_ADMIN_PASSWORD`, `INITIAL_ORGANIZATION_NAME`).
            *   `[x]` (`start.sh`) Save user choices to `sagrafacile_config.json`.
            *   `[x]` (`start.sh`) Generate `.env` file from `sagrafacile_config.json`.
            *   `[x]` (`start.bat`) Implement logic to check for `sagrafacile_config.json`.
            *   `[x]` (`start.bat`) Add interactive prompts for `MY_DOMAIN`, `CLOUDFLARE_API_TOKEN`, database credentials, `JWT_SECRET`.
            *   `[x]` (`start.bat`) Add prompt for demo data seeding (`SAGRAFACILE_SEED_DEMO_DATA`) or initial admin/org setup (`INITIAL_ADMIN_EMAIL`, `INITIAL_ADMIN_PASSWORD`, `INITIAL_ORGANIZATION_NAME`).
            *   `[x]` (`start.bat`) Save user choices to `sagrafacile_config.json`.
            *   `[x]` (`start.bat`) Generate `.env` file from `sagrafacile_config.json`.
        *   `[x]` **API Backend (`Program.cs` & `InitialDataSeeder.cs`) Adjustments:**
            *   `[x]` Implement logic in `InitialDataSeeder.cs` to check `SAGRAFACILE_SEED_DEMO_DATA`.
            *   `[x]` If `false` (or not set), `InitialDataSeeder.cs` uses `INITIAL_ORGANIZATION_NAME`, `INITIAL_ADMIN_EMAIL`, `INITIAL_ADMIN_PASSWORD` to attempt initial setup (if no user-defined orgs exist).
            *   `[x]` Passwords for SuperAdmin and DemoUser are configurable via `SUPERADMIN_PASSWORD` and `DEMO_USER_PASSWORD` environment variables.
        *   `[x]` **Deployment ZIP Package:**
            *   `[x]` Define contents for the distributable ZIP (excluding source code, including scripts, `docker-compose.yml`, `Caddyfile`, `docs/`, `sagrafacile_config.json.example`, printer service installer).
        *   `[x]` **Automated GitHub Release Packaging:**
            *   `[x]` Create GitHub Actions workflow (`.github/workflows/release-zip.yml`) to build the ZIP and create a release on new version tags.
        *   `[x]` **Documentation Updates:**
            *   `[x]` Update `README.md` installation instructions for the new interactive `start.sh` and `start.bat` scripts and Cloudflare Let's Encrypt setup.
            *   `[x]` Update `DEPLOYMENT_ARCHITECTURE.md` to reflect interactive `start.sh` and `start.bat` scripts, `sagrafacile_config.json`, API changes for seeding, and ZIP package contents.
            *   `[x]` Ensure service name consistency (`api` for backend) across `docker-compose.yml`, `Caddyfile`, and docs.

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

### Phase 10: Android Wrapper App (See `docs/AndroidWrapperAppArchitecture.md`)

*   **Goal:** Develop a lightweight native Android application that embeds the SagraFacile PWA. This app will handle local DNS resolution internally, allowing Android users to connect to the SagraFacile server using its domain name and trusted SSL certificate without needing to change device DNS settings.
*   **Key Tasks:**
    *   `[x]` **Project Setup:**
        *   `[x]` Choose development approach (Native Kotlin).
        *   `[x]` Set up Android Studio project (`sagrafacile-androidapp`).
        *   `[x]` Install and configure JDK.
    *   `[x]` **Core Wrapper Implementation:**
        *   `[x]` Implement MainActivity with a full-screen WebView.
        *   `[x]` Configure WebView settings (JavaScript, DOM Storage, modern back button handling).
        *   `[x]` Implement Settings Activity for user to input and save the SagraFacile server's local IP address and domain name (using SharedPreferences).
        *   `[x]` Implement custom `WebViewClient` with `shouldInterceptRequest` override:
            *   `[x]` Intercept requests to the configured SagraFacile domain.
            *   `[x]` Re-route to the saved local IP, preserving path/query.
            *   `[x]` Set `Host` HTTP header to the configured SagraFacile domain for SSL validation.
            *   `[x]` Return `WebResourceResponse` with data from the local server.
    *   `[x]` **Application Shell (Partial):**
        *   `[x]` Add app icon (using Image Asset Studio).
        *   `[ ]` Add splash screen.
        *   `[x]` Configure `AndroidManifest.xml` (permissions, activities).
    *   `[x]` **Testing (Initial):**
        *   `[x]` Test PWA functionality within the wrapper.
        *   `[x]` Verify SSL connection is trusted.
        *   `[x]` Test IP and domain configuration and persistence.
    *   `[ ]` **Build & Distribution:**
        *   `[ ]` Generate signed release APK.
        *   `[ ]` Prepare for direct distribution (e.g., download link).
    *   `[ ]` **Documentation:**
        *   `[x]` Create `docs/AndroidWrapperAppArchitecture.md`. (Already completed)
        *   `[x]` Create `sagrafacile-androidapp/ProjectMemory.md` and log initial setup.
        *   `[ ]` Update main `README.md` with instructions for using the Android Wrapper App.
    *   `[ ]` **Further Enhancements:**
        *   `[ ]` Add menu item in `MainActivity` to re-open `SettingsActivity`.
        *   `[ ]` Enhance error handling in `CustomWebViewClient` (e.g., custom error page).
        *   `[ ]` Review and confirm `app_name` in `strings.xml`.


        *   `[ ]` Review and confirm `app_name` in `strings.xml`.

### Phase 11: SaaS (Software-as-a-Service) Platform

*   **Goal:** Develop and launch a fully managed, commercial SaaS offering of SagraFacile to provide a zero-installation, easy-to-use version of the product for non-technical users.
*   **Key Features & Tasks:**
    *   `[x]` **SaaS User Onboarding Flow:**
        *   `[x]` Implement a new user sign-up process with mandatory email confirmation and consent to Terms of Service/Privacy Policy.
        *   `[x]` Develop a guided "Onboarding Wizard" for new users to create their organization and select a subscription plan. (Organization creation and redirection implemented, subscription plan selection pending)
    *   `[~]` **Billing & Subscription Integration:**
        *   `[x]` Implement Subscription Management UI (`/admin/subscription`).
        *   `[ ]` Integrate with a payment provider (e.g., Stripe).
        *   `[ ]` Implement a "Trial Tier" with usage-based limits (e.g., limited orders per day).
        *   `[ ]` Implement a "Pay-Per-Day" pricing model for paid plans.
    *   `[ ]` **Platform Administration & Security:**
        *   `[ ]` Deprecate the `SuperAdmin` role to enhance security and GDPR compliance.
        *   `[ ]` Develop a separate, secure "Platform Admin" application for internal management of tenants and subscriptions.
        *   `[ ]` Implement a dedicated, secure API for platform administration tasks.
    *   `[x]` **First-Time Use Wizard:**
        *   `[x]` Implement a universal "First-Time Setup" wizard for all new organizations (both SaaS and self-hosted) to guide admins through creating their first Area, Printer, Cashier Station, and Menu Category.
        *   `[x]` Created comprehensive wizard with 6 steps: Welcome, Area, Printer, Cashier Station, Menu, and Completion.
        *   `[x]` Enhanced workflow explanations in both wizard (`sagrafacile-webapp/src/components/admin/setup-wizard/StepArea.tsx`) and admin area page (`sagrafacile-webapp/src/app/app/org/[orgId]/admin/areas/page.tsx`) to better explain operational options.
        *   `[x]` Implemented GUID generation for Windows USB printers in the wizard.
        *   `[x]` Integrated wizard into admin dashboard (`sagrafacile-webapp/src/app/app/org/[orgId]/admin/page.tsx`) with automatic detection for new organizations (if no areas exist).
    *   `[x]` **Password Reset Flow (SaaS Only):**
        *   `[x]` **Backend:**
            *   `[x]` Implement logic to generate and store a secure, time-limited password reset token.
            *   `[x]` Add a public API endpoint to request a password reset email.
            *   `[x]` Add a public API endpoint to validate the token and update the user's password.
            *   `[x]` Integrate with `IEmailService` to send the reset link.
        *   `[x]` **Frontend:**
            *   `[x]` Add a "Forgot Password?" link to the login page.
            *   `[x]` Create a page to request the password reset.
            *   `[x]` Create a page to handle the reset link, allowing the user to enter a new password.
    *   `[x]` **Invitation-Based User Management (SaaS Only):**
        *   `[x]` **Backend:**
            *   `[x]` Create a new `UserInvitation` entity to store invitation tokens, target email, and roles.
            *   `[x]` Modify `AccountService` to create invitations and send invitation emails.
            *   `[x]` Add a public API endpoint for an invited user to accept the invitation and complete their registration (setting their own password).
            *   `[x]` Update `RegisterUserAsync` to handle the invitation acceptance flow.
        *   `[x]` **Frontend:**
            *   `[x]` Modify the "Gestione Utenti" page to have an "Invita Utente" (Invite User) button instead of "Aggiungi Utente" for SaaS mode.
            *   `[x]` Create a dialog for admins to enter an email and select roles for the invitation.
            *   `[x]` Create a new public page for invited users to complete their sign-up.
            *   `[x]` Implement UI for viewing and revoking pending invitations.

### Phase 12: Data Lifecycle & GDPR Compliance

*   **Goal:** Implement robust features for user and organization data deletion and export, ensuring compliance with GDPR principles like the "Right to Erasure" and "Right to Portability".
*   **Key Tasks:**
    *   `[~]` **User Deletion (Soft & Hard/Anonymization):**
        *   `[x]` **Backend:**
            *   `[x]` Add a `Status` field (e.g., `Active`, `Deleted`) to the `User` model and create migration.
            *   `[x]` Implement a "soft delete" (SaaS) / "hard delete" (self-hosted) mechanism in `AccountService`.
            *   `[x]` Create a background job (`DataRetentionService`) to anonymize personal data of soft-deleted users after a 30-day grace period.
            *   `[x]` Add a secure `DELETE /api/users/{userId}` endpoint.
        *   `[ ]` **Frontend:**
            *   `[ ]` Add a "Delete User" button and confirmation dialog to the User Management page.
            *   `[ ]` Ensure the UI correctly displays "Deleted User" for historical records (e.g., orders created by a deleted user).
    *   `[~]` **Organization Deletion (Soft & Hard):**
        *   `[x]` **Backend:**
            *   `[x]` Add `Status` (`Active`, `PendingDeletion`) and `DeletionScheduledAt` fields to the `Organization` model and create migration.
            *   `[x]` Implement a "soft delete" (SaaS) / "hard delete" (self-hosted) mechanism in `OrganizationService`.
            *   `[x]` Create a background job (`DataRetentionService`) to perform a hard, cascading delete of the organization and all its associated data after a 30-day grace period.
            *   `[x]` Add a secure `DELETE /api/organizations/{orgId}` endpoint, potentially requiring multi-factor confirmation (e.g., typing org name).
        *   `[ ]` **Frontend:**
            *   `[ ]` Create a "Danger Zone" in the organization settings page.
            *   `[ ]` Implement a "Delete Organization" button with a multi-step confirmation modal.
    *   `[ ]` **Data Export (Right to Portability):**
        *   `[ ]` **Backend:**
            *   `[ ]` Implement a background job triggered by an admin request to export all organization data (Orders, Menu, Users, etc.) into a structured format (e.g., a ZIP file of multiple JSONs).
            *   `[ ]` Implement an email notification service to send a secure, time-limited download link to the admin once the export is ready.
            *   `[ ]` Add a `POST /api/organizations/{orgId}/export` endpoint to trigger the export process.
        *   `[ ]` **Frontend:**
            *   `[ ]` Add an "Export All Data" button to the "Danger Zone" in organization settings.
            *   `[ ]` Display appropriate feedback to the user (e.g., "Your data export has started. You will receive an email when it's complete.").
    *   `[x]` **Documentation:**
        *   `[x]` Update `privacy-policy.md` and `terms-of-service.md` to reflect the new data deletion and export procedures.

*(This roadmap is a living document and will be updated as the project progresses.)*

# Project Memory - SagraFacile.NET Backend & Services

# How to work on the project
*   **Technology:** ASP.NET Core (.NET 9), Entity Framework Core, PostgreSQL.
*   **Architecture:** RESTful API following a Service Layer Pattern. Designed for multi-tenancy (data isolation per organization).
*   **Authentication/Authorization:** Uses ASP.NET Core Identity with JWT for authentication. Implement role-based authorization checks primarily within the Service layer (using `BaseService` helpers) and supplement with `[Authorize]` attributes on controllers where appropriate.
*   **Real-time:** Utilizes SignalR (`Hubs/OrderHub.cs`) for real-time communication with clients (Frontend, Companion Apps). Inject `IHubContext<OrderHub>` into services to broadcast messages.
*   **Database:** Use EF Core for data access. Apply schema changes via EF Core migrations (`dotnet ef migrations add`, `dotnet ef database update`). Ensure migrations are robust.
*   **Testing:** Write Integration Tests (`SagraFacile.NET.API.Tests.Integration`) for API endpoints and service logic. Manual testing by the user is also crucial for workflow validation. Aim for comprehensive integration test coverage.
*   **Memory:** Update this `ProjectMemory.md` file at the end of each session, summarizing accomplishments, key decisions, identified issues, and agreed-upon next steps. Reference `DataStructures.md` and `ApiRoutes.md` when discussing models or endpoints.
*   **Code Style:** Follow standard C# and ASP.NET Core conventions. Use dependency injection throughout.

---
# Session Summaries (Newest First)

## (2025-06-12) - Refactor Printer Document Builder to use ESCPOS_NET
**Context:** Addressed issues with printing special characters (e.g., Euro symbol, accented characters appearing as '?') and QR codes being too small. This was suspected to be due to encoding problems in the custom `EscPosDocumentBuilder` and limitations in its QR code generation.
**Accomplishments:**
*   **Added `ESCPOS_NET` NuGet Package:** The `ESCPOS_NET` library (version 3.0.0) was added to the `SagraFacile.NET.API.csproj`.
*   **Refactored `SagraFacile.NET/SagraFacile.NET.API/Utils/EscPosDocumentBuilder.cs`:**
    *   The class was rewritten to utilize the `ESCPOS_NET` library.
    *   It now uses `ESCPOS_NET.Emitters.EPSON` for generating printer commands.
    *   The default printer code page is set to `CodePage.PC858_EURO` in the constructor to ensure correct rendering of European special characters.
    *   The `PrintQRCode` method was updated to use `_emitter.PrintQRCode()`, mapping the previous `moduleSizeMapping` parameter to `ESCPOS_NET.Size2DCode` (e.g., `Size2DCode.EXTRA` or `Size2DCode.LARGE`) to address the small QR code issue. The error correction level is set to `CorrectionLevel2DCode.PERCENT_15`.
    *   Font size adjustments now use `_emitter.SetStyles()` with `PrintStyle.DoubleWidth` and `PrintStyle.DoubleHeight`.
    *   The `SetDoubleStrike` method was found to not have a direct equivalent in the `PrintStyle` enum or easily accessible via the `EPSON` emitter's style methods; it currently logs a warning and is non-functional to prevent compilation errors.
**Key Decisions:**
*   Adopted `ESCPOS_NET` as the standard library for ESC/POS command generation to improve reliability and cross-platform compatibility.
*   Prioritized fixing character encoding and QR code size.
**Next Steps:**
*   Thoroughly test printing functionality, especially receipts with Euro symbols, accented characters, and QR codes, to ensure the issues are resolved.
*   Review `PrinterService.cs` to ensure it aligns with any subtle changes in `EscPosDocumentBuilder` usage, particularly around QR code sizing parameters if the default mapping isn't optimal.
*   If `SetDoubleStrike` is a critical feature, further investigation into `ESCPOS_NET` capabilities or printer-specific raw commands will be needed.

## (2025-06-12) - Configurable PreOrder Polling Service
**Context:** Added the ability to enable or disable the `PreOrderPollingBackgroundService` (which polls SagraPèreOrdini) via an environment variable.
**Accomplishments:**
*   **`.env.example` Updated:** Added `ENABLE_PREORDER_POLLING_SERVICE` variable with a default of `true` and descriptive comments.
*   **`docker-compose.yml` Updated:** The `backend` service now includes `ENABLE_PREORDER_POLLING_SERVICE: ${ENABLE_PREORDER_POLLING_SERVICE:-true}` in its environment configuration, ensuring the variable is passed to the .NET application and defaults to `true` if not explicitly set in the `.env` file.
*   **`SagraFacile.NET/SagraFacile.NET.API/Program.cs` Modified:**
    *   The `PreOrderPollingBackgroundService` is now conditionally registered.
    *   The application reads the `ENABLE_PREORDER_POLLING_SERVICE` configuration value.
    *   If the value is `true` or not present (defaulting to `true` due to docker-compose), the service is registered.
    *   If the value is explicitly `false`, the service is not registered.
    *   Added `Console.WriteLine` statements to log whether the service is enabled or disabled at startup for clarity.
**Key Decisions:**
*   The polling service is enabled by default to maintain existing behavior unless explicitly disabled.
*   Clear logging at application startup indicates the status of the polling service.

## (2025-06-11) - Shifted to Pre-built Docker Image Deployment Strategy
**Context:** Based on user reflection and agreement, the project's deployment strategy has been fundamentally changed from building Docker images on the user's machine to distributing pre-built Docker images via a container registry. This aims to simplify user setup, improve reliability, and speed up deployment.
**Accomplishments (Overall Project):**
*   **`docker-compose.yml` Updated:** Modified the main `docker-compose.yml` to use `image:` directives (with placeholders for actual image names like `yourdockerhub_username/sagrafacile-api:latest`) for the `backend` and `frontend` services, instead of `build:` directives.
*   **New Helper Scripts Created:**
    *   `start.bat` & `start.sh`: For starting SagraFacile services. They now run `docker-compose up -d` which will pull images.
    *   `update.bat` & `update.sh`: For updating SagraFacile. They run `docker-compose pull` and then `docker-compose up -d`.
    *   `stop.bat` & `stop.sh`: For stopping SagraFacile services using `docker-compose down`.
*   **Documentation Updated:**
    *   `README.md`: The "Docker Deployment & Installation Guide" section was significantly rewritten to reflect the new, simpler process: download ZIP, configure `.env`, run `start` script.
    *   `DEPLOYMENT_ARCHITECTURE.md`: Updated to describe the pre-built image strategy, the new role of Dockerfiles (developer/CI artifact), changes to the deployment package contents, and the updated user setup workflow.
**Key Decisions:**
*   The SagraFacile backend and frontend applications will be distributed as pre-built Docker images hosted on a container registry (e.g., Docker Hub, GitHub Container Registry).
*   End-users will download a small package containing `docker-compose.yml`, `Caddyfile`, `.env.example`, and helper scripts.
*   This change significantly simplifies the end-user experience, removing the need for local source code or build environments.
**Next Steps (Developer):**
*   Set up a container registry (e.g., Docker Hub or GitHub Container Registry).
*   Implement a CI/CD pipeline (e.g., GitHub Actions) to automatically build and push tagged Docker images for the backend (`SagraFacile.NET.API`) and frontend (`sagrafacile-webapp`) to the chosen registry upon code changes.
*   Replace placeholder image names in `docker-compose.yml` with actual image paths from the registry.
*   Update the `README.md` and `DEPLOYMENT_ARCHITECTURE.md` with the actual GitHub Releases page URL and container registry image names once available.
*   Finalize the distributable ZIP package contents as per `DEPLOYMENT_ARCHITECTURE.md`.

## (2025-06-11) - Initiated Docker-Based Deployment Setup (Backend Aspects)
**Context:** Began implementing a comprehensive Docker-based deployment strategy for SagraFacile, aiming for a guided manual setup for end-users.
**Accomplishments:**
*   **Deployment Architecture Documented:** Created `DEPLOYMENT_ARCHITECTURE.md` detailing the 5-phase plan, core technologies (Docker, Docker Compose, Caddy), and setup workflows for Windows, macOS, and Linux.
*   **Backend Dockerfile (`SagraFacile.NET/SagraFacile.NET.API/Dockerfile`):** Verified and confirmed corrections to project names (from `SagraPOS.NET.API` to `SagraFacile.NET.API`) and paths within the Dockerfile, ensuring it aligns with the current project structure. Confirmed it exposes port 8080 for HTTP.
*   **Docker Compose (`docker-compose.yml`):** Created the main `docker-compose.yml` file defining services for `db` (PostgreSQL), `backend` (.NET API), `frontend` (Next.js), and `caddy`. Configured build contexts, environment variables (including direct construction of `ConnectionStrings__DefaultConnection`), volumes, and dependencies. Set `container_name` for all services.
*   **Caddyfile:** Created the `Caddyfile` to manage HTTPS via `local_certs`, redirect HTTP to HTTPS, and reverse proxy requests to the `backend:8080` and `frontend:3000` services.
*   **`.env.example`:** Created the example environment file (`.env.example`) with placeholders for database credentials, JWT secrets, and other necessary configurations.
**Key Decisions:**
*   The backend service within Docker will listen on port 8080 (HTTP), and Caddy will handle external HTTPS termination and proxying.
*   A specific container name (`sagrafacile_caddy`) will be used for Caddy to simplify CA certificate extraction commands.
*   The `docker-compose.yml` directly constructs the backend's connection string from environment variables.
*   **Helper Scripts Created:** `setup.bat` and `setup.sh` were created in the repository root to guide users through the Docker Compose setup, including `.env` configuration and Caddy CA certificate installation.
*   **Main README Updated:** The main `README.md` in the repository root was significantly updated with a comprehensive "Docker Deployment & Installation Guide," incorporating details from `DEPLOYMENT_ARCHITECTURE.md` and instructions for using the new setup scripts and trusting the Caddy CA.
**Next Steps (Overall Deployment Plan):**
*   Manually package all components (source code, Dockerfiles, `docker-compose.yml`, `Caddyfile`, `.env.example`, setup scripts, `README.md`) into a distributable `.zip` file (Task 4.3).
*   Finalize the Windows Printer Service application:
    *   Ensure it's configurable for the SagraFacile backend URL.
    *   Ensure it trusts the custom root CA.
*   Create a Windows Installer (e.g., Inno Setup) for the `SagraFacile.WindowsPrinterService` (Task 5.2).
*   Update main documentation further if needed regarding the printer service installer (Task 5.3).

## (2025-06-11) - Resolved Static Web Asset Conflict After Project Rename
**Context:** A build error occurred in the `SagraFacile.NET.API` project: "Conflicting assets with the same target path ... from different projects." This happened after the project was renamed from `SagraPOS.NET.API` to `SagraFacile.NET.API`. The build system was still detecting assets related to the old project name (`_content/SagraPOS.NET.API`) alongside the new one (`_content/SagraFacile.NET.API`), specifically for a Bootstrap CSS file.
**Accomplishments:**
*   Investigated the `.csproj` file, which showed no direct references to the old project name.
*   Attempted `dotnet clean` on the solution, which did not resolve the issue.
*   **Resolved the conflict by manually deleting the `obj` and `bin` directories within the `SagraFacile.NET/SagraFacile.NET.API/` project folder and then rebuilding the project.** This forced a complete regeneration of build artifacts, eliminating the stale references to the old project name.
**Key Decisions:**
*   The standard `dotnet clean` was insufficient to clear all problematic intermediate build files after the project rename. A manual deletion of `obj` and `bin` was necessary.
**Next Steps:**
*   The project now builds successfully.

## (2025-06-11) - Fixed VS Code Launch Configuration for .NET API
**Context:** After renaming the .NET project from `SagraPOS.NET.API` to `SagraFacile.NET.API`, the VS Code launch configuration (F5) was broken, showing the error "Configuration 'C#: SagraPOS.NET.API [https]' is missing in 'launch.json'".
**Accomplishments:**
*   **`.vscode/launch.json` Updated (in the root of the SagraPOS repository):**
    *   Added a new launch configuration specifically for the `SagraFacile.NET.API` project.
    *   The new configuration is named `"C#: SagraFacile.NET.API [https]"`.
    *   It uses `type: "coreclr"`, `request: "launch"`, and `preLaunchTask: "build"`.
    *   The `cwd` is set to `"${workspaceFolder}/SagraFacile.NET/SagraFacile.NET.API"`.
    *   It correctly references the `"https"` profile from the project's `Properties/launchSettings.json` file (`launchSettingsProfile: "https"` and `launchSettingsFilePath: "${workspaceFolder}/SagraFacile.NET/SagraFacile.NET.API/Properties/launchSettings.json"`).
**Key Decisions:**
*   The new launch configuration was tailored to use the existing "https" profile from the `SagraFacile.NET.API/Properties/launchSettings.json` to ensure consistency with how the project is intended to be run during development.
*   The naming convention "C#: ProjectName [profileName]" was maintained for the new launch configuration.
**Next Steps:**
*   User to test launching the `SagraFacile.NET.API` project using F5 in VS Code with the new "C#: SagraFacile.NET.API [https]" configuration.


## (2025-06-09) - Fixed Public Display Authentication Errors
**Context:** The public-facing Queue Display and Order Pickup Display pages were failing due to authentication errors. The Queue Display was calling an authenticated-only endpoint, and the Pickup Display's endpoint was triggering an internal service that required user context.
**Accomplishments:**
*   **`SagraFacile.NET/SagraFacile.NET.API/Controllers/QueueController.cs` Modified:**
    *   Removed the `GET /api/areas/{areaId}/queue/state` endpoint, as it was incorrectly placed behind an `[Authorize]` attribute.
*   **`SagraFacile.NET/SagraFacile.NET.API/Controllers/PublicController.cs` Modified:**
    *   Added a new public endpoint `GET /api/public/areas/{areaId}/queue/state` to serve the queue status without authentication.
    *   Updated the existing `GET /api/public/areas/{areaId}/orders/ready-for-pickup` endpoint to call a new, public-safe service method.
*   **`SagraFacile.NET/SagraFacile.NET.API/Services/Interfaces/IOrderService.cs` Updated:**
    *   Added a new method signature `GetPublicOrdersByStatusAsync(int areaId, OrderStatus status)` for fetching orders without requiring an authenticated user context.
*   **`SagraFacile.NET/SagraFacile.NET.API/Services/OrderService.cs` Updated:**
    *   Implemented the new `GetPublicOrdersByStatusAsync` method, which calls a new public method in the `DayService` to get the current open day.
*   **`SagraFacile.NET/SagraFacile.NET.API/Services/Interfaces/IDayService.cs` Updated:**
    *   Added a new method signature `GetPublicCurrentOpenDayAsync(int organizationId)`.
*   **`SagraFacile.NET/SagraFacile.NET.API/Services/DayService.cs` Updated:**
    *   Implemented the new `GetPublicCurrentOpenDayAsync` method, which retrieves the current open day for an organization without performing user authorization checks.
**Key Decisions:**
*   Moved the queue state endpoint to the `PublicController` to correctly align its accessibility with its purpose.
*   Created new, parallel service methods (`GetPublic...`) to handle data retrieval for public endpoints, avoiding modifications to existing internal methods that correctly require user context. This prevents regressions in authenticated parts of the application.

## (2025-06-09) - Finalized and Debugged Dynamic Ad Carousel
**Context:** Completed the full implementation of the dynamic advertising carousel, including debugging several issues related to media serving and display.
**Accomplishments:**
*   **Enabled Static File Serving:** Added `app.UseStaticFiles()` to `Program.cs` to allow the backend to serve media files directly from the `wwwroot` directory.
*   **Avoided Ad Blockers:** Changed the file storage path from `/media/ads` to `/media/promo` in `AdMediaItemService.cs` to prevent client-side ad blockers from interfering with image loading.
*   **Created Public API Endpoint:** Added a new public endpoint `GET /api/public/areas/{areaId}/ads` to `PublicController.cs` to serve active promotional media to the public-facing display. This involved injecting the `IAdMediaItemService`.

## (2025-06-08) - Implemented Guest and Takeaway Charges
**Context:** The user wanted to add the ability to configure a per-guest "coperto" (cover charge) and a per-order "asporto" (takeaway) fee.
**Accomplishments:**
*   **`SagraFacile.NET/SagraFacile.NET.API/Models/Area.cs` Updated:**
    *   Added `GuestCharge` and `TakeawayCharge` decimal properties to the `Area` entity.
    *   Created and applied the `AddGuestAndTakeawayChargesToArea` EF Core migration.
*   **DTOs Updated:**
    *   Added `guestCharge` and `takeawayCharge` to `AreaDto.cs` and `AreaUpsertDto.cs`.
*   **`SagraFacile.NET/SagraFacile.NET.API/Services/AreaService.cs` Updated:**
    *   Updated the mapping logic in `GetAllAreasAsync`, `GetAreaByIdAsync`, `GetAreaBySlugsAsync`, and `UpdateAreaAsync` to include the new charge properties.
*   **`SagraFacile.NET/SagraFacile.NET.API/Services/OrderService.cs` Updated:**
    *   Modified `CreateOrderAsync` and `ConfirmPreOrderPaymentAsync` to add the `GuestCharge` (multiplied by `NumberOfGuests`) or the `TakeawayCharge` to the order's `TotalAmount` based on the `IsTakeaway` flag.
*   **`SagraFacile.NET/SagraFacile.NET.API/Services/PrinterService.cs` Updated:**
    *   Modified `PrintOrderDocumentsAsync` and `ReprintOrderDocumentsAsync` to display the new charges as separate line items on the printed receipt for clarity.
**Key Decisions:**
*   The charges are configured at the `Area` level, providing flexibility for different operational zones.
*   The logic correctly distinguishes between applying the guest charge for dine-in orders and the takeaway charge for takeaway orders.

## (2025-06-08) - Increased QR Code Size on Receipts
**Context:** The QR codes printed on receipts were too small, causing difficulty for cameras to scan them reliably.
**Accomplishments:**
*   **`SagraFacile.NET/SagraFacile.NET.API/Services/PrinterService.cs` Modified:**
    *   Investigated the `EscPosDocumentBuilder` utility and identified that the `PrintQRCode` method accepts a `moduleSize` parameter to control the QR code's dot size.
    *   Updated the calls to `docBuilder.PrintQRCode(...)` in both the `PrintOrderDocumentsAsync` (for initial receipts) and `ReprintOrderDocumentsAsync` (for reprints) methods.
    *   The `moduleSize` argument was explicitly set to `6`, effectively doubling it from the default value of `3`.
**Key Decisions:**
*   Increased the QR code size directly via the existing `moduleSize` parameter in the `EscPosDocumentBuilder` to ensure a simple and reliable change. This should improve scannability without altering the QR code's content.
changed from string to byte[] the prinitng. euro symbol still not prinitng correctly. qr code still to small!!!

## (2025-06-07) - Refactored Comanda Printing for Mobile & Waiter-Confirmed Orders
**Context:** Completed the backend implementation for the mobile table ordering feature by ensuring comanda printing logic is consistent between implicitly confirmed mobile orders and explicitly confirmed waiter orders.
**Accomplishments:**
*   **`SagraFacile.NET/SagraFacile.NET.API/Services/OrderService.cs` Refactored:**
    *   **Extracted Printing Logic:** Created a new private helper method, `TriggerDeferredComandaPrintingAsync(Order order)`, which encapsulates the logic for printing comandas to category-specific printers. This method correctly checks if a comanda was already printed at a cashier station before proceeding, preventing duplicates.
    *   **Refactored `ConfirmOrderPreparationAsync`:** The existing comanda printing block was replaced with a single call to the new `TriggerDeferredComandaPrintingAsync` helper method.
    *   **Updated `CreateOrderAsync`:** Added a call to `TriggerDeferredComandaPrintingAsync` within the logic that handles mobile table orders (i.e., when an order is created with a `TableNumber` and `WaiterId` is set). This ensures that mobile orders that bypass the `Paid` status now trigger the same comanda printing logic as orders confirmed manually by a waiter.
**Key Decisions:**
*   Centralized the deferred comanda printing logic into a single, reusable method to eliminate code duplication and ensure workflow consistency.
*   This change guarantees that whether an order's preparation is confirmed implicitly (via mobile table order) or explicitly (via waiter app), the comanda printing follows the identical, correct procedure.

## (2025-06-07) - Implemented Mobile Table Order Backend Logic
**Context:** Aligned the backend with the `MobileTableOrderingArchitecture.md` to support the new frontend table ordering interface.
**Accomplishments:**
*   **`SagraFacile.NET/SagraFacile.NET.API/Services/OrderService.cs` Updated:**
    *   Modified `CreateOrderAsync` to handle orders submitted with a `TableNumber`.
    *   If an order includes a `TableNumber` and the corresponding `Area` has `EnableWaiterConfirmation` set to `true`, the system now bypasses the `OrderStatus.Paid` state.
    *   The order status is advanced directly to `Preparing` (if KDS is enabled), `ReadyForPickup`, or `Completed`, effectively treating the submission as an implicit waiter confirmation.
    *   The `WaiterId` on the `Order` is now set to the ID of the user creating the table order.
*   **`SagraFacile.NET/SagraFacile.NET.API/DTOs/CreateOrderDto.cs` Updated:**
    *   Added a nullable `string? TableNumber` property to the DTO to receive the table number from the client.
    *   Added a nullable `int? CashierStationId` property to the DTO.
**Key Decisions:**
*   The backend now intelligently progresses the order status for table-side orders, streamlining the workflow as per the architectural design.
*   The user creating the order from the mobile interface is considered both the cashier and the confirming waiter in this specific flow.

## (2025-06-06) - Enhanced WindowsPrinterService UI/UX and Connection Logic
**Context:** Improved the `SagraFacile.WindowsPrinterService` companion app for better usability, clearer status feedback, and more robust connection handling, in preparation for debugging USB thermal printer functionality.
**Accomplishments:**
*   **`SettingsForm.Designer.cs` (UI Layout & Controls):**
    *   Localized all user-facing text (labels, buttons, group titles) to Italian.
    *   Added `btnGenerateGuid` ("Genera GUID") button for easier Instance GUID creation.
    *   Added `lblStatus` label within a group box to display real-time SignalR connection status (color-coded).
    *   Added `btnTestPrinter` ("Test Stampante") button to allow direct printer testing from the settings form.
    *   Updated placeholder text for "URL Base Server" (`txtHubUrl`) to expect a full base URL (e.g., `https://server:port`).
    *   Adjusted control layout and sizes for clarity.
*   **`SettingsForm.cs` (UI Logic):**
    *   Implemented `UpdateConnectionStatus(string statusMessage, Color statusColor)` for thread-safe updates to `lblStatus`.
    *   Added `SignalRServiceInstance` property (with `[Browsable(false)]` and `[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]`) to hold a reference to the `SignalRService`.
    *   Modified `ButtonSave_Click` to be `async void` and to call `SignalRServiceInstance.RestartAsync()` after saving settings, to apply new connection parameters immediately. Validation messages and save confirmations are now in Italian.
    *   Implemented `BtnTestPrinter_Click` event handler: retrieves selected printer, creates test data, and calls `SignalRServiceInstance.TestPrintAsync()`. Displays results in Italian.
    *   Ensured event handlers for new buttons are wired up.
    *   Added necessary `using` directives (`SagraFacile.WindowsPrinterService.Services`, `System.ComponentModel`, `System.Threading`).
*   **`Services/SignalRService.cs` (Connection & Core Logic):**
    *   **URL Parsing:** `StartAsync` now correctly interprets the Hub URL setting as a full base URL and appends `/api/orderhub`. Includes improved validation.
    *   **Certificate Handling (Dev):** Enabled `ServerCertificateCustomValidationCallback` to bypass SSL certificate validation for development/testing, with a prominent warning log.
    *   **Status Reporting:**
        *   `SetSettingsForm()` method allows `ApplicationLifetimeService` to link the `SettingsForm` instance.
        *   `OnConnectionStatusChanged()` now updates the linked `SettingsForm`'s status label with Italian messages and appropriate colors (via `DetermineColorForStatus`).
    *   **Restart Capability:** Added `public async Task RestartAsync(CancellationToken cancellationToken)` to stop and then restart the SignalR connection (used after settings are saved).
    *   **Test Print Capability:** Added `public async Task<bool> TestPrintAsync(string printerName, string testData)` to send raw test data to a specified printer via `_rawPrinter`.
    *   Status messages passed to `OnConnectionStatusChanged` (and thus to the UI) are now in Italian.
*   **`ApplicationLifetimeService.cs` (Orchestration):**
    *   Modified `OnSettingsClicked()` to set both `_signalRService.SetSettingsForm(settingsForm)` (for status updates from service to form) and `settingsForm.SignalRServiceInstance = _signalRService` (for actions from form to service like restart).
**Key Decisions:**
*   Centralized printer testing logic within `SignalRService` as it already holds the `IRawPrinter` dependency.
*   Made SignalR service restart automatically upon saving settings to immediately apply changes.
*   Prioritized Italian localization for all user-facing elements in the Settings Form.
*   Implemented a development-only SSL certificate bypass with clear warnings.
**Next Steps:**
*   User to build and test the `SagraFacile.WindowsPrinterService` thoroughly:
    *   Verify UI localization and new controls.
    *   Test SignalR connection with correct base URL (e.g., `https://<IP>:<PORT>`).
    *   Confirm status label updates correctly (connecting, connected, registered, disconnected, errors).
    *   Test "Genera GUID", "Test Stampante", and saving settings (triggering service restart).
*   If connection issues persist, use breakpoints in `SignalRService.cs` (e.g., in `StartAsync` after reading config, after URL construction, in `ConnectWithRetriesAsync` catch block) and in `SettingsForm.cs` (`GetSignalRConfig`).
    *   Once the companion app's UI/UX and connection are stable, resume debugging USB thermal printer functionality.

## (2025-06-06) - Resolved JsonException in Printer Assignments API
**Context:** Encountered a `System.Text.Json.JsonException` (object cycle detected) when retrieving printer assignments via the `GET /api/Printers/{printerId}/assignments` endpoint. This was due to circular references between `Printer` and `PrinterCategoryAssignment` entities during serialization.
**Accomplishments:**
*   **`PrinterCategoryAssignmentDto.cs` Created (`SagraFacile.NET/SagraFacile.NET.API/DTOs/`):**
    *   Introduced a new DTO (`PrinterCategoryAssignmentDto`) to represent printer assignments specifically for API responses. This DTO includes `PrinterId`, `MenuCategoryId`, `MenuCategoryName`, and `MenuCategoryAreaId`, effectively flattening the structure and avoiding the circular reference to the `Printer` entity.
*   **`IPrinterAssignmentService.cs` Updated:**
    *   Modified the `GetAssignmentsForPrinterAsync` method signature to return `Task<IEnumerable<PrinterCategoryAssignmentDto>>` instead of `Task<IEnumerable<PrinterCategoryAssignment>>`.
*   **`PrinterAssignmentService.cs` Updated:**
    *   Adjusted the `GetAssignmentsForPrinterAsync` implementation to:
        *   Use the new `PrinterCategoryAssignmentDto`.
        *   Modify the LINQ query to select and map the `PrinterCategoryAssignment` entities (with their related `MenuCategory` details) into `PrinterCategoryAssignmentDto` instances. This ensures only the necessary, non-cyclical data is returned.
**Key Decisions:**
*   Opted for a targeted DTO approach to resolve the serialization cycle instead of a global `JsonSerializerOptions` change (e.g., `ReferenceHandler.Preserve`), to avoid potential unintended side effects in other parts of the API.
**Next Steps:**
*   User to test the `GET /api/Printers/{id}/assignments?areaId={areaId}` endpoint to confirm the `JsonException` is resolved and the data is returned as expected.

## (2025-06-06) - Resolved .NET Build Error on macOS
**Context:** Encountered a build error `NETSDK1100` when attempting to build the `SagraFacile.WindowsPrinterService` project on macOS.
**Accomplishments:**
*   **`SagraFacile.WindowsPrinterService.csproj` Updated:**
    *   Added `<EnableWindowsTargeting>true</EnableWindowsTargeting>` to the main `<PropertyGroup>` to allow building the Windows-targeted project on a non-Windows OS. This resolves the `NETSDK1100` error.

**Next Steps:**
*   Attempt to build the project again to confirm the fix.

## (Next Session) - Planned Work
**Context:** Current session paused debugging of USB thermal printer due to issues with the `SagraFacile.WindowsPrinterService` companion app's registration with the SignalR hub.
**Next Steps:**
1.  **Enhance `SagraFacile.WindowsPrinterService` (Companion App):**
    *   Improve the UI/UX for displaying connection status to the SignalR hub.
    *   Provide a clearer way to configure the necessary settings (SignalR Hub URL, Printer GUID).
    *   Implement better logging within the companion app to aid troubleshooting.
2.  **Resume USB Thermal Printer Debugging:**
    *   Once the companion app is improved and its connection/registration can be reliably verified, continue debugging the USB thermal printer functionality.
    *   Focus on ensuring the companion app correctly registers with the `OrderHub` using the matching GUID.
    *   Verify print jobs are dispatched and received by the companion app.


---
# Historical Sessions (Condensed)

## Stock Management (Backend - 2025-06-04)
*   **Summary:** Implemented backend for Stock Management (`Scorta` property on `MenuItem`, related DTO updates, `MenuItemService` enhancements for stock operations like `UpdateStockAsync`, `ResetStockAsync`, `ResetAllStockForAreaAsync`, and `OrderService` updates for transactional stock checks/decrements during order creation/confirmation and SignalR broadcasts for stock changes). New `MenuItemsController` endpoints were added for stock management. An EF Core migration (`AddScortaToMenuItem`) was created.
*   **Key Decisions:** `Scorta = null` signifies unlimited quantity. Stock checks and decrements are transactional. SignalR broadcasts `StockUpdateBroadcastDto` for real-time updates.
*   **Outcome:** Frontend implementation for Admin UI and Cashier UI followed.

## DisplayOrderNumber Frontend Fixes & KDS Check (Note - 2025-06-04)
*   **Summary:** Noted frontend fixes for `DisplayOrderNumber` and a verification task for the backend to ensure `KdsOrderDto` correctly populates `displayOrderNumber` for KDS stations.
*   **Outcome:** User to test frontend; backend KDS DTO verification pending.

## Display Order Number (Backend - 2025-06-03)
*   **Summary:** Implemented the backend for the "Display Order Number" feature. This included adding a `DisplayOrderNumber` property to the `Order` model, creating the `AreaDayOrderSequence` entity to manage unique daily sequences per area, updating `ApplicationDbContext`, and creating the `AddDisplayOrderNumber` EF Core migration. `OrderService` was enhanced to generate these numbers during order creation/confirmation, and DTOs (`OrderDto`, `OrderStatusBroadcastDto`) were updated. `PrinterService` was modified to use the new display number. The internal `Order.Id` was changed to use GUIDs for new orders.
*   **Key Decisions:** Internal `Order.Id` remains PK (now GUID). `DisplayOrderNumber` generated upon order activation (create/confirm pre-order), prefixed by `Area.Slug`. QR codes on receipts continue to use internal `Order.Id`.
*   **Outcome:** Frontend UI updates and testing followed in subsequent sessions.

## Automatic Token Refresh (Backend - 2025-05-29)
*   **Summary:** Implemented a JWT refresh token strategy. Created `TokenResponseDto` and `RefreshTokenRequestDto`. Updated the `User` model with `RefreshToken` and `RefreshTokenExpiryTime` (migration `AddUserRefreshTokens`). `AccountService` was enhanced to generate, store, and validate refresh tokens, implementing token rotation. `LoginUserAsync` now returns both tokens, and `RefreshTokenAsync` handles renewal. A new public endpoint `POST /api/accounts/refresh-token` was added to `AccountsController`. Token lifetimes were made configurable.
*   **Key Decisions:** Standard JWT refresh token flow with token rotation. Short-lived access tokens (e.g., 15 mins), longer-lived refresh tokens (e.g., 7 days) stored in the database.
*   **Outcome:** Frontend integration to handle token storage, refresh calls on 401s, and session management was completed.

## Public Order Pickup Display (Backend - 2025-05-29)
*   **Summary:** Implemented backend components for the Public Order Pickup Display. This involved creating `OrderStatusBroadcastDto` for SignalR. `OrderService.SendOrderStatusUpdateAsync` was refactored to broadcast this DTO. A new method `OrderService.GetOrdersByStatusAsync(areaId, status)` was added to fetch orders for the display's initial load (filtered by current open `DayId`), with enhanced logging for debugging. A new public endpoint `GET /api/public/areas/{areaId}/orders/ready-for-pickup` was added to `PublicController.cs`. The `OrderHub` utilizes existing group mechanisms for client subscriptions. The "Planned Public Order Pickup Display" session's items were incorporated into this implementation.
*   **Outcome:** Frontend implementation for the public display and staff confirmation pages followed. Debugging of `GetOrdersByStatusAsync` related to `DayId` filtering was noted as an ongoing concern based on frontend testing.

## Customer Queue System (Backend - "Previous Date" prior to 2025-05-29)
*   **Summary:** Completed the backend implementation for the Customer Queue System. This included the `AreaQueueState` entity, `Area.EnableQueueSystem` flag, and EF Core migration `AddQueueManagement`. DTOs (`CalledNumberDto`, `CalledNumberBroadcastDto`, `QueueStateDto`) and `ServiceResult` were created. `IQueueService` and `QueueService.cs` were implemented with logic for managing queue state, calling numbers, and admin operations, including SignalR broadcasts (`QueueNumberCalled`, `QueueReset`, `QueueStateUpdated`). A `QueueController.cs` was added with API endpoints and role-based authorization.
*   **Key Decisions:** Used optimistic concurrency for state updates. `CallSpecificAsync` updates 'last called' but not `NextSequentialNumber`. Admin actions require `OrgAdmin`/`SuperAdmin`, Cashier actions allow `Cashier` role.
*   **Outcome:** Frontend UI implementation for Cashier and Public Display followed. User was tasked to apply the database migration.

## Customer Queue System - Initial Models (Various Dates)
*   Implemented `AreaQueueState` entity and added `EnableQueueSystem` flag to `Area`.
*   Configured database context and relationships for the queue system.

## Configurable Printing Architecture (Various Dates)
*   **Admin-Selected Printer for Reprints:** Modified `ReprintRequestDto` and `PrinterService` to allow optional admin override for reprint printer destination.
*   **Order Service Integration:** Integrated `IPrinterService` into `OrderService`, refined logic for triggering receipt and comanda prints based on workflow states and configuration flags (`PrintComandasAtThisStation`, `PrintComandasAtCashier`), preventing duplicate comanda prints.
*   **Reprint Functionality & Item Notes:** Implemented backend reprint API (`/api/orders/{orderId}/reprint`) with consolidated comandas. Ensured `OrderItem.Note` is included on receipts and comandas. Updated pre-order confirmation DTO/logic.
*   **ESC/POS Generation:** Implemented detailed ESC/POS generation using `EscPosDocumentBuilder` in `PrinterService` for receipts, comandas (refactored with helpers), and test prints, replacing dummy data.
*   **SignalR & Print Dispatch:** Enhanced `OrderHub` to manage Windows printer client connections (`RegisterPrinterClient`, `_printerConnections`). Implemented `PrinterService` logic to determine target printers based on rules (station, area, category) and dispatch jobs via TCP/IP (Network) or SignalR (WindowsUsb).
*   **Printer Configuration Backend (Phase 1):** Established initial database models (`Printer`, `PrinterCategoryAssignment`, `PrinterType`), DTOs, basic CRUD services (`PrinterService`), and API endpoints (`PrintersController`) for managing printers.

## Windows Printer Service (2024-07-26)
*   **Refactored to SignalR:** Replaced WebSocket with SignalR (`SignalRService`) for backend communication. Simplified configuration (removed registration token, simplified Hub URL). Utilized raw Windows printing via P/Invoke (`RawPrinterHelperService`). Updated settings UI and documentation.
*   **User Tasks Defined:** Specified necessary project file updates and UI changes for the Windows Printer Service refactoring.
*   **Backend Alignment:** Confirmed `OrderHub` and `PrinterService` align with the simplified Windows service registration and `PrintJob` message format.

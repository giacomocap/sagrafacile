# Project Memory - SagraFacile.NET Backend & Services

---
# Session Summaries (Newest First)

## (2025-07-07) - Enhanced Windows Printer Service UI/UX and Logging
**Context:** Addressed the next step in improving the `SagraFacile.WindowsPrinterService` companion app by enhancing UI/UX for connection status and settings, and implementing comprehensive logging for better debugging of USB thermal printer issues.
**Accomplishments:**
*   **Enhanced PrintStationForm UI:**
    *   **Added Profile and Printer Information Labels:** Added `lblProfileName` and `lblPrinterName` labels to `PrintStationForm.Designer.cs` to display the currently active profile name and configured printer name.
    *   **Improved Connection Status Display:** Modified `UpdateConnectionStatusLabel()` to use `SignalRService.GetCurrentStatus()` for consistent status message and color display.
    *   **Re-introduced Structured Logging:** Added `ILogger<PrintStationForm>` dependency injection and enhanced logging in `btnPrintNext_Click()` and other methods for better debugging.
    *   **Updated Form Layout:** Repositioned controls to accommodate new labels and improved visual hierarchy.
*   **Enhanced SignalRService:**
    *   **Exposed ActiveProfileSettings:** Added public `ActiveProfileSettings` property to allow `PrintStationForm` to access printer configuration details.
    *   **Maintained Existing Events:** Confirmed `ConnectionStatusChanged` and `OnDemandQueueCountChanged` events are properly implemented for real-time UI updates.
*   **Significantly Enhanced RawPrinterHelperService Logging:**
    *   **Detailed Parameter Validation:** Added comprehensive null/empty checks for printer names and data with specific error logging.
    *   **Data Length and Content Logging:** Added logging of data length, hex preview of ESC/POS commands, and string content preview for debugging.
    *   **Enhanced Error Reporting:** Implemented detailed Win32 error code logging with hexadecimal format and human-readable error descriptions for common printer-related error codes (ERROR_FILE_NOT_FOUND, ERROR_ACCESS_DENIED, ERROR_PRINTER_NOT_FOUND, etc.).
    *   **Success Confirmation:** Added detailed success logging with data size confirmation.
*   **Build Verification:** Confirmed all changes compile successfully with only minor warnings (package compatibility and async method warning).
**Key Decisions:**
*   Prioritized comprehensive logging and error reporting to facilitate debugging of USB thermal printer registration and printing issues.
*   Enhanced UI to provide clear visibility into the active profile, printer configuration, and connection status.
*   Maintained backward compatibility while improving debugging capabilities.
*   Used structured logging patterns for better log analysis and troubleshooting.
**Outcome:** The Windows Printer Service now provides significantly better visibility into its operation through enhanced UI and comprehensive logging. This will greatly facilitate debugging of USB thermal printer issues, particularly around SignalR registration, print job dispatch, and receipt verification.
**Next Steps:**
*   Resume USB thermal printer debugging with the enhanced logging and UI improvements.
*   Focus on verifying correct registration with `OrderHub` and print job dispatch/receipt verification.
*   Test the enhanced UI and logging with actual printer configurations.

## (2025-07-03) - Implemented SaaS Onboarding Wizard - Organization Provisioning (Backend)
**Context:** Implemented the backend components for the first step of the SaaS Onboarding Wizard, allowing newly registered users to create their organization.
**Accomplishments:**
*   **`OrganizationProvisionRequestDto`:** Created a new DTO for the organization provisioning request.
*   **`IOrganizationService`:** Added `ProvisionOrganizationAsync` method to the interface.
*   **`OrganizationService`:**
    *   Implemented `ProvisionOrganizationAsync` to handle the creation of a new `Organization` and associate the current user with it, assigning the 'Admin' role.
    *   Updated `GenerateSlug` and introduced `GenerateUniqueSlugAsync` to ensure organization slugs are unique by appending a number if a collision is found.
    *   Injected `UserManager<User>` to manage user updates (assigning `OrganizationId` and roles).
*   **`OrganizationsController`:** Added a new `POST /api/organizations/provision` endpoint, protected by authentication, to expose the provisioning functionality. It retrieves the user ID from claims and handles `ServiceResult` responses.
**Key Decisions:**
*   The provisioning process is transactional to ensure data consistency (organization creation, user update, role assignment).
*   Slugs are automatically generated and guaranteed unique, preventing conflicts.
*   The endpoint is authenticated but does not require a specific role, allowing any newly registered user to provision their organization.
**Outcome:** The backend is now ready to support the organization creation step of the SaaS onboarding wizard.

## (2025-07-03) - Implemented SaaS User Registration and Email Confirmation
**Context:** As the first step in building the SaaS onboarding flow, the backend needed to support a public, unauthenticated sign-up process with mandatory email verification.
**Accomplishments:**
*   **`IAccountService`:** Added a new `ConfirmEmailAsync` method to the interface.
*   **`AccountService` Refactoring:**
    *   Injected `IEmailService` to handle sending confirmation emails.
    *   The `RegisterUserAsync` method was significantly updated to differentiate between a public SaaS sign-up and an admin-initiated user creation based on the `APP_MODE` environment variable and whether the API call is authenticated.
    *   For public SaaS sign-ups, a `User` is now created with `EmailConfirmed = false` and a `null` `OrganizationId`.
    *   After user creation, it generates an email confirmation token and sends a verification link to the user's email address.
    *   The `LoginUserAsync` method was updated to prevent users from logging in if their email has not been confirmed.
*   **`AccountsController`:** A new public, anonymous `GET /api/accounts/confirm-email` endpoint was added. This endpoint receives the `userId` and `token` from the confirmation link and calls the `ConfirmEmailAsync` service method to verify the user's email address.
**Key Decisions:**
*   The registration logic now clearly separates the public SaaS flow from the internal, admin-driven flow.
*   Email confirmation is a mandatory step for all new public sign-ups, enhancing security and user data validity.
*   The system is designed to be resilient; even if the confirmation email fails to send, the user account is still created and they can request a new link later.
**Outcome:** The backend is now fully equipped to handle the initial phase of SaaS user onboarding, from registration to email verification.

## (2025-07-03) - Implemented SaaS Mode Framework and Subscription Status Endpoint
**Context:** To support the dual Open Core and SaaS model, a foundational framework was needed to differentiate behavior. This session focused on creating the local testing infrastructure and the initial API endpoints required for SaaS-specific features.
**Accomplishments:**
*   **Created `docker-compose.saas-local.yml`:** A new Docker Compose file was added to the `sagrafacile` repository. This file inherits from the standard `docker-compose.yml` but crucially adds the `APP_MODE: saas` environment variable to the API service, enabling local development and testing of SaaS features without affecting the default open-source setup.
*   **Created `InstanceController`:** A new, unauthenticated API endpoint `GET /api/instance/info` was created. This endpoint returns the current application mode (`saas` or `opensource`), allowing the frontend to dynamically adjust its UI and features.
*   **Exposed `SubscriptionStatus` via API:**
    *   The `OrganizationDto` was updated to include the `SubscriptionStatus` field.
    *   The `IOrganizationService` interface and `OrganizationService` implementation were updated to ensure that `GetAllOrganizationsAsync` and `GetOrganizationByIdAsync` correctly map and return the `SubscriptionStatus` in the `OrganizationDto`.
    *   The `OrganizationsController`'s `GetOrganization` endpoint was updated to return the `OrganizationDto`, securely exposing the subscription status to authorized frontend components.
**Key Decisions:**
*   The use of a separate `docker-compose.saas-local.yml` file is a clean and explicit way to manage different development environments, fully aligning with the "Phase 1: Local SaaS Simulation" strategy.
*   Exposing the instance mode via a dedicated endpoint provides a clear and secure mechanism for the frontend to adapt its behavior.
*   Returning DTOs from the API endpoints, rather than full data models, is a security best practice that gives us precise control over what data is exposed.
**Outcome:** The backend is now fully equipped with the foundational logic to support a dual-mode (Open Source vs. SaaS) operation. The frontend can now query the instance mode and retrieve subscription-related data for specific organizations.

## (2025-07-03) - Major Database Migration: OrganizationId from int to Guid
**Context:** A critical and complex database migration was required to change the `OrganizationId` primary key from an `int` to a `Guid` across the entire database. This change was necessary to ensure globally unique identifiers for organizations, a prerequisite for future SaaS features. The task also included adding a new `SubscriptionStatus` field to the `Organization` table.
**Accomplishments:**
*   **Troubleshot `HostAbortedException`:**
    *   **Problem:** The `dotnet ef migrations add` command was consistently failing with a `HostAbortedException`.
    *   **Root Cause Analysis:** Discovered that this exception is *expected behavior* for EF Core design-time tools. The tool starts the host to gather information and then immediately aborts it. Our generic `catch (Exception ex)` block was incorrectly treating this as a fatal error.
    *   **Solution:** Refined the `catch` block in `Program.cs` to specifically ignore the `HostAbortedException` and any exception originating from `Microsoft.EntityFrameworkCore.Design`, while still catching other unexpected startup errors.
*   **Implemented a Data-Safe Migration Script:**
    *   **Problem:** The default migration generated by EF Core (`AlterColumn`) does not handle the conversion of existing data and would have failed on databases with existing organizations.
    *   **Solution:** Manually edited the generated migration file (`ConvertOrganizationIdToGuidAndAddSubscription.cs`) to perform a safe, multi-step data migration using raw SQL.
    *   **The script now performs the following steps:**
        1.  Creates a temporary PostgreSQL function (`temp_int_to_guid`) to generate deterministic, repeatable `Guid` values from the old `int` IDs.
        2.  Drops all foreign key constraints referencing the `OrganizationId`.
        3.  Drops the `IDENTITY` property from the `Organizations.Id` column, which was a blocking issue.
        4.  Alters the `Organizations.Id` primary key and all `OrganizationId` foreign keys to the `uuid` type, using the temporary function to convert the existing integer values on the fly.
        5.  Re-creates all the foreign key constraints using the new `uuid` columns.
        6.  Drops the temporary function.
*   **Applied Migration Successfully:** After implementing the correct exception handling and the robust migration script, the `dotnet ef database update` command was executed successfully, applying the schema and data changes to the development database without any data loss.
**Key Decisions:**
*   The `HostAbortedException` from EF Core tools should be explicitly ignored during application startup.
*   Complex data type changes on primary/foreign keys with existing data require manual, raw SQL migration scripts to ensure data integrity. Using a deterministic function to map old IDs to new GUIDs is crucial.
*   The final migration script is idempotent and safe to run on both new (empty) and existing databases, making it suitable for all deployment environments.
**Outcome:** The database schema has been successfully and safely updated. The `OrganizationId` is now a `Guid` throughout the application, and the `SubscriptionStatus` field is available, paving the way for future development.

## (2025-06-26) - Refactored SignalRService Architecture & Fixed PDF Printing Issues
**Context:** Addressed a critical PDF printing issue where webapp test prints to HTML/PDF printers were failing, and completely refactored the SignalRService which had grown to over 600 lines and violated the Single Responsibility Principle.
**Accomplishments:**
*   **Fixed PDF Printing Issue:**
    *   **Root Cause:** The previous `PdfPrintingService` relied on unreliable external processes (Adobe Reader, Windows shell execute) to print PDFs, leading to "file not found" errors and print failures. Additionally, the `SignalRService` was missing the `contentType` parameter when sending print jobs.
    *   **Solution:**
        *   **Windows Printer Service:** Replaced the fragile external process approach in `PdfPrintingService.cs` with direct, programmatic PDF printing using the `PdfiumViewer` library. This involved adding `PdfiumViewer` and `PdfiumViewer.Native.x86_64.v8-xfa` NuGet packages to `SagraFacile.WindowsPrinterService.csproj`. The `PrintPdfAsync` method now loads PDF data from a memory stream and prints it directly, honoring specified paper sizes.
        *   **Backend API:** Ensured `PrinterService.cs` correctly includes the `contentType` parameter (`"application/pdf"` or `"application/vnd.escpos"`) in the SignalR `PrintJob` message sent to the Windows Printer Service.
    *   **Result:** Resolved the "Nessuna applicazione associata al file specificato per questa operazione" error and enabled reliable PDF printing from the webapp.
*   **Major SignalRService Refactoring:**
    *   **Created Three New Specialized Services:**
        *   **`PdfPrintingService`** - Now handles all PDF printing logic using `PdfiumViewer` for robust, direct printing.
        *   **`PrinterConfigurationService`** - Manages fetching printer configuration from the backend API with proper error handling and SSL bypass for development.
        *   **`PrintJobManager`** - Manages the print job queue for on-demand printing, including queue operations and job processing.
    *   **Refactored SignalRService:**
        *   Updated constructor to inject the three new services via dependency injection.
        *   Replaced configuration fetching logic to use `PrinterConfigurationService.FetchConfigurationAsync()`.
        *   Updated print job handling to use `PrintJobManager.EnqueueJob()` and `PrintJobManager.ProcessJobAsync()`.
        *   Delegated queue management to use `PrintJobManager.DequeueJob()` and `PrintJobManager.GetQueueCount()`.
        *   Removed over 200 lines of duplicate code including old PDF printing methods, static HttpClient, and local queue management.
    *   **Enhanced PrintJobItem Model:**
        *   Added `ContentType` property to support both ESC/POS and PDF content types.
        *   Added `PaperSize` property to use profile-configured paper sizes for PDF printing.
        *   Updated all PrintJobItem creation to pass content type and paper size from profile settings.
    *   **Updated Dependency Injection:**
        *   Registered all new services in `Program.cs` with proper singleton patterns where appropriate for shared state.
**Key Decisions:**
*   Prioritized robustness and reliability for PDF printing by adopting `PdfiumViewer` for direct programmatic control.
*   Applied SOLID principles to break down the monolithic SignalRService into focused, single-responsibility services.
*   Maintained backward compatibility while improving code organization and testability.
*   Used profile-configured paper sizes for PDF printing to ensure proper document formatting.
*   Implemented proper error handling and logging throughout the new service architecture.
**Outcome:** 
*   **PDF printing issue resolved** - Webapp test prints to HTML/PDF printers now work correctly and reliably.
*   **Significantly improved architecture** - The codebase now follows SOLID principles with better separation of concerns.
*   **Enhanced maintainability** - Each service has a clear purpose and can be modified independently.
*   **Better testability** - Services can be unit tested in isolation.
*   **Improved extensibility** - Easy to add new printing methods or configuration sources.
*   **Reduced code duplication** - Eliminated redundant PDF printing logic and queue management code.

## (2025-06-26) - Implemented HTML/PDF Test Print & Fixed SignalR Message Format
**Context:** Addressed an issue where test prints for HTML/PDF printers were failing due to missing templates and an incorrect SignalR message format to the Windows Printer Service.
**Accomplishments:**
*   **Created Test Print HTML Template:**
    *   Added `SagraFacile.NET/SagraFacile.NET.API/PrintTemplates/Html/test-print.html`.
    *   This template provides a detailed, professional-looking test document for HTML/PDF printers.
*   **Enhanced `PrinterService.cs`:**
    *   Implemented `GenerateTestHtmlContent()` method to dynamically load and populate the new `test-print.html` template using Scriban.
    *   Added `GenerateFallbackTestHtml()` for robustness if the embedded template is not found.
    *   Introduced `CreateSampleOrderForTest()` to provide necessary sample data for PDF generation during test prints.
    *   **`Models/Printer.cs`:** Added a nullable `PaperSize` string property to the `Printer` entity.
    *   **Database Migration:** Created and applied the `AddPaperSizeToPrinter` migration to update the database schema.
    *   **DTOs:** Added the `PaperSize` property to `PrinterDto.cs` and `PrinterUpsertDto.cs`.
    *   **`Services/PrinterService.cs`:** Updated the service to handle the `PaperSize` property during CRUD operations and to pass it to the `PdfService`.
    *   **`Services/Interfaces/IPdfService.cs`:** Updated the interface to accept an optional `paperSize` parameter.
    *   **`Services/PdfService.cs`:**
        *   The `CreatePdfFromHtmlAsync` method now accepts the `paperSize` parameter.
        *   Added logic to convert the paper size string (e.g., "A5") into the corresponding `PaperFormat` enum required by PuppeteerSharp.
        *   The generated PDF now uses the specified paper size, resolving the original printing issue.
*   **Troubleshooting:**
    *   Resolved a `HostAbortedException` during `dotnet ef` tool execution by temporarily commenting out Serilog and Puppeteer download logic in `Program.cs` to isolate the issue, which was related to the tool's interaction with the application host builder. The migration was successfully applied once the root cause was managed.
**Key Decisions:**
*   Paper size is now a configurable option for both local test prints (via Windows profiles) and server-side PDF generation (via Admin UI).
*   The system gracefully handles unsupported paper sizes by logging a warning and using the default.
**Outcome:** The application now correctly handles different paper sizes for standard printers, ensuring that documents like receipts and comandas are printed correctly without being cut off.

## (2025-06-26) - Integrated Profile Name Field in Windows Printer Service Settings
**Context:** Modified the Windows Printer Service to include the profile name as a field within the settings form instead of using a separate dialog window, improving the user experience by providing a unified interface.
**Accomplishments:**
*   **Windows Printer Service (`SagraFacile.WindowsPrinterService`):**
    *   **`SettingsForm.Designer.cs`:** Added a new "Nome Profilo" label and text field at the top of the form, repositioned all other controls accordingly, and increased the form height to accommodate the new field.
    *   **`SettingsForm.cs`:**
        *   Updated `PopulateControlsFromSettings()` to populate the profile name field from the current settings.
        *   Completely refactored `SaveProfileSettings()` to get the profile name from the text field instead of using `InputDialogForm`.
        *   Added validation for empty profile names with proper focus management.
        *   Added logic to handle profile renaming (automatically deletes old profile file when name changes).
        *   Updated `ButtonSave_Click()` to validate the profile name field before proceeding with save.
    *   **`ProfileSelectionForm.cs`:** Minor updates to pass the parent form as owner when opening SettingsForm dialogs for better modal behavior.
    *   **`Program.cs`:** Removed the unnecessary `InputDialogForm` registration from the dependency injection container since it's no longer used.
*   **Removed Dependencies:** The `InputDialogForm` is no longer used anywhere in the application, eliminating the separate dialog approach entirely.
**Key Decisions:**
*   Unified the profile management interface by integrating the profile name field directly into the main settings form.
*   Implemented proper validation and error handling for profile names, including duplicate name detection.
*   Added support for profile renaming with automatic cleanup of old profile files.
*   Maintained backward compatibility with existing profile files while improving the user experience.
**Outcome:** The Windows Printer Service now provides a streamlined, single-form interface for all profile settings, eliminating the need for separate dialogs and improving the overall user experience. Profile creation, editing, and renaming are now handled seamlessly within the unified settings interface.

## (2025-06-25) - Implemented Print Template Management & UI
**Context:** Implemented the full CRUD, default restoration, and preview functionality for print templates, including backend API, service logic, and frontend UI. Also addressed dialog overflow issues.
**Accomplishments:**
*   **Backend (.NET API):**
    *   **Print Templates:**
        *   Created `PrintTemplateService.cs` (`IPrintTemplateService`) for managing print templates (CRUD, restore defaults, generate preview).
        *   Created `PrintTemplatesController.cs` with API endpoints for all template operations (`GET`, `POST`, `PUT`, `DELETE`, `restore-defaults`, `preview`).
        *   Registered `IPrintTemplateService` in `Program.cs`.
        *   Embedded default HTML templates (`receipt.html`, `comanda.html`) as resources in the API assembly for the "Restore Defaults" feature.
    *   **DTOs:** Created `PrintTemplateDto`, `PrintTemplateUpsertDto`, `PreviewRequestDto` and `QueryParameters` (reused/adapted).
*   **Frontend (Next.js WebApp):**
    *   **Service:** Updated `printTemplateService.ts` with new API calls for `restoreDefaultTemplates` and `previewTemplate`.
    *   **Types:** Added `PreviewRequestDto` to `src/types/index.ts`.
    *   **UI Components:**
        *   Created `PrintTemplatePreviewDialog.tsx` to display PDF previews generated by the backend.
        *   Updated `src/app/app/org/[orgId]/admin/print-templates/page.tsx` to:
            *   Add a "Ripristina Default" (Restore Defaults) button.
            *   Add an "Anteprima" (Preview) action to the dropdown menu for HTML/PDF templates, which opens the `PrintTemplatePreviewDialog`.
    *   **UI Fixes:**
        *   Modified `PrintTemplatePreviewDialog.tsx` to use a flex column layout (`flex flex-col`) and `flex-1` on the content area, ensuring the PDF iframe scrolls and the footer buttons remain visible.
        *   Modified `PrintTemplateFormDialog.tsx` to use a flex column layout (`flex flex-col`) and `max-h-[90vh]` on the dialog content, with `overflow-y-auto flex-1` on the form fields container, ensuring the form scrolls and the footer buttons remain visible.
**Key Decisions:**
*   Centralized print template management in a dedicated backend service and controller.
*   Leveraged Puppeteer Sharp for robust HTML-to-PDF conversion on the backend for previews.
*   Implemented client-side dialogs with proper flexbox and overflow handling to prevent UI elements from becoming unreachable with long content.
**Outcome:** The system now provides a comprehensive and user-friendly interface for managing and previewing print templates, with improved UI stability for dialogs.

## (2025-06-23) - Enhanced Order Filtering and Optional Pagination
**Context:** Addressed feedback regarding order filtering on the waiter page and refined the pagination logic to be optional, allowing clients to fetch all items if pagination parameters are omitted.
**Accomplishments:**
*   **Backend (.NET API):**
    *   **Order Query Parameters:** Modified `SagraFacile.NET.API/DTOs/OrderQueryParameters.cs` to make `Page` and `PageSize` nullable (`int?`) to support optional pagination. Added `Statuses` property (`List<int>?`) to allow filtering orders by multiple statuses.
    *   **Order Service:** Updated `SagraFacile.NET.API/Services/OrderService.cs` (`GetOrdersAsync`) to conditionally apply pagination (Skip/Take) only if `Page` and `PageSize` are provided. Implemented filtering by the `Statuses` list if it's present in the query parameters. Adjusted `PaginatedResult` construction to correctly reflect total items when pagination is not applied.
**Key Decisions:**
*   Made pagination truly optional on the backend, allowing clients to retrieve all items by omitting `page` and `pageSize`.
*   Introduced status-based filtering for orders via the `Statuses` query parameter, enabling more granular control over fetched data.
**Outcome:** The backend API is now more flexible for order retrieval, supporting both paginated and full-list fetches, and allowing filtering by order statuses.

## (2025-07-07) - Fixed SaaS Onboarding Redirection & Token Refresh
**Context:** Addressed a critical issue where newly provisioned SaaS users were not being redirected to their organization dashboard because the frontend's JWT token was not updated with the new `organizationId` claim.
**Accomplishments:**
*   **`AccountService.cs` (`RefreshTokenAsync`):** Confirmed that the backend's `RefreshTokenAsync` method correctly generates a new JWT token with the user's latest claims (including `organizationId`) from the database.
**Key Decisions:**
*   Leveraged the existing refresh token mechanism to ensure updated user claims are propagated to the frontend.
**Outcome:** The backend is correctly providing updated tokens. The frontend changes (documented in `sagrafacile-webapp/ProjectMemory.md`) now correctly utilize this.

## (2025-06-20) - Resolved JWT Authentication Issues
**Context:** After a fresh server deployment, users could log in, but subsequent API calls failed with `SecurityTokenSignatureKeyNotFoundException` (401 Unauthorized). This indicated a mismatch in JWT configuration between token generation and validation.
**Accomplishments:**
*   **Standardized JWT Configuration:**
    *   Modified `SagraFacile.NET.API/Services/AccountService.cs` (token generation) to use `_configuration["JWT_SECRET"]`, `_configuration["JWT_ISSUER"]`, and `_configuration["JWT_AUDIENCE"]` directly. This aligns with how these values are set as top-level environment variables by `start.sh` and `docker-compose.yml`. Previously, it was attempting to read `Jwt:Key`, `Jwt:Issuer`, and `Jwt:Audience` from a nested configuration section.
    *   Modified `SagraFacile.NET.API/Program.cs` (token validation middleware) to also use `builder.Configuration["JWT_ISSUER"]` and `builder.Configuration["JWT_AUDIENCE"]` directly, instead of `builder.Configuration["Jwt:Issuer"]` and `builder.Configuration["Jwt:Audience"]`. The `JWT_SECRET` was already correctly configured here.
*   **Removed Serilog (Temporary Debugging Step):**
    *   To isolate issues during API startup, Serilog integration was temporarily commented out in `SagraFacile.NET.API/Program.cs`. Standard `Console.WriteLine` and `ILoggerFactory` were used for logging. This helped confirm that migrations and data seeding were running.
*   **Ensured Demo Data Seeding Variables:**
    *   Updated `docker-compose.yml` to explicitly pass `SAGRAFACILE_SEED_DEMO_DATA`, `INITIAL_ORGANIZATION_NAME`, `INITIAL_ADMIN_EMAIL`, `INITIAL_ADMIN_PASSWORD`, `SUPERADMIN_EMAIL`, `SUPERADMIN_PASSWORD`, and `DEMO_USER_PASSWORD` environment variables to the `api` service. This ensures the `InitialDataSeeder` behaves as expected based on the `.env` file generated by `start.sh`.
**Key Decisions:**
*   Consolidated JWT configuration to rely solely on top-level environment variables (`JWT_SECRET`, `JWT_ISSUER`, `JWT_AUDIENCE`) for both token generation and validation, ensuring consistency across the application and deployment scripts.
*   Temporarily removed Serilog to simplify debugging of API startup and data seeding processes.
**Outcome:** With these changes, JWT tokens should be generated and validated using the same consistent secret, issuer, and audience, resolving the 401 errors. The demo data seeding should also work correctly based on user choices during `start.sh` execution.
**Next Steps:**
*   User to test the deployment after these changes are applied.
*   (Future) Re-integrate Serilog once the core functionality is stable.

## (2025-06-16) - Enhanced Windows Printer Service Packaging and Autostart
**Context:** To improve the deployment and usability of the `SagraFacile.WindowsPrinterService`, it needed to be packaged as a self-contained executable and support profile-specific autostart functionality.
**Accomplishments:**
*   **Command-Line Profile Loading:**
    *   Modified `SagraFacile.WindowsPrinterService/Program.cs` to parse a `--profile-guid <GUID>` command-line argument.
    *   If a valid profile GUID is provided, the application now loads that specific profile directly, bypassing the `ProfileSelectionForm`.
*   **In-App Autostart Management:**
    *   Added `AutoStartEnabled` boolean property to `SagraFacile.WindowsPrinterService/Models/ProfileSettings.cs`.
    *   Updated `SagraFacile.WindowsPrinterService/SettingsForm.cs`:
        *   A new checkbox (`chkAutoStart`) allows users to enable/disable autostart for the current profile.
        *   The state of this checkbox is saved to the profile's JSON file.
        *   When saved, the form now calls `StartupManager.SetAutoStart()` to create or delete a shortcut in the Windows Startup folder.
    *   Created `SagraFacile.WindowsPrinterService/Utils/StartupManager.cs`:
        *   This new static class contains the `SetAutoStart(ProfileSettings profile, bool enable)` method.
        *   It uses `IWshRuntimeLibrary` (Windows Script Host Object Model) to create/delete `.lnk` shortcuts in `Environment.SpecialFolder.Startup`.
        *   The shortcut target includes the `--profile-guid` argument to launch the specific profile.
    *   Added the required COMReference for `IWshRuntimeLibrary` to `SagraFacile.WindowsPrinterService.csproj`.
*   **Self-Contained Executable Packaging (GitHub Actions):**
    *   Modified `.github/workflows/release-zip.yml`:
        *   Added `actions/setup-dotnet@v4` to ensure the correct .NET SDK version.
        *   Added a `dotnet publish` step to build `SagraFacile.WindowsPrinterService` as a self-contained, single-file executable (`SagraFacilePrinter.exe`) for `win-x64`.
        *   The `SagraFacilePrinter.exe` is now copied into the main `SagraFacile-${VERSION}-dist.zip`.
        *   `SagraFacilePrinter.exe` is also added as a separate asset to the GitHub Release.
        *   Updated the release notes body in the workflow to describe the new executable and its features.
*   **Documentation:**
    *   Updated `SagraFacile.NET/README.md` with a new section detailing the Windows Printer Service, its features (including autostart and command-line launch), packaging, and usage.
**Key Decisions:**
*   The Windows Printer Service will be distributed as a self-contained `.exe` for ease of use, requiring no separate .NET runtime installation by the user.
*   Autostart functionality is managed within the application on a per-profile basis, providing users with granular control.
*   The GitHub Actions release workflow now fully automates the build and packaging of this service alongside the main application.
**Outcome:** The Windows Printer Service is now easier to deploy and more user-friendly, with robust autostart capabilities for specific printer configurations.

## (2025-06-16) - Configured GitHub Actions for Release Packaging & Versioning Strategy
**Context:** To automate the creation of distributable ZIP packages for new releases and establish a clear versioning strategy for SagraFacile, making deployment easier.
**Accomplishments:**
*   **Versioning Strategy Adopted:** Semantic Versioning (`MAJOR.MINOR.PATCH`) will be used. Releases are triggered by Git tags prefixed with `v` (e.g., `v1.0.0`).
*   **GitHub Actions Workflow Created (`.github/workflows/release-zip.yml`):**
    *   A new workflow was implemented to automate the release packaging process.
    *   It triggers on pushes to tags matching the `v*.*.*` pattern.
    *   The workflow checks out the code, extracts the version from the Git tag, and then packages essential deployment files into a `SagraFacile-${VERSION}-dist.zip` archive.
    *   The packaged files include: `docker-compose.yml`, `Caddyfile`, `.env.example`, all `start/stop/update` scripts (`.sh` and `.bat`), `README.md`, `LICENSE.txt`, the newly created `sagrafacile_config.json.example`, and the entire `docs/` directory.
    *   Finally, the workflow uses the `softprops/action-gh-release` action to create a new GitHub Release, automatically attaching the generated ZIP file as a release asset.
*   **`sagrafacile_config.json.example` File Added:**
    *   A new example file, `sagrafacile_config.json.example`, was created in the project root. This file serves as a template for the `sagrafacile_config.json` that is generated by the interactive `start.sh` and `start.bat` scripts.
    *   This example file is now included in the distributable ZIP package created by the GitHub Action.
*   **Documentation Updated:**
    *   `Roadmap.md`: Relevant tasks in "Phase 7: Deployment & Monitoring" concerning the deployment ZIP package and automated GitHub Release packaging were marked as complete.
    *   `DEPLOYMENT_ARCHITECTURE.md`: Task 4.3 (Package for Distribution) was updated to note the inclusion of `sagrafacile_config.json.example` in the ZIP. Task 4.4 (Create GitHub Actions Workflow) was marked as implemented.
**Key Decisions:**
*   The release process is now highly automated, triggered by a simple `git tag` push.
*   The distributable ZIP contains all necessary configuration files and scripts for users to get started, excluding source code.
*   The `SagraFacile.WindowsPrinterService.Setup.exe` is noted as a separate artifact to be potentially added to releases manually or via a different build process.
**Outcome:** SagraFacile now has an automated mechanism for creating versioned release packages, significantly streamlining the distribution process and adhering to standard versioning practices.

## (2025-06-16) - Implemented Interactive Setup Scripts (`start.sh` & `start.bat`) & Updated Documentation
**Context:** To simplify the deployment process for SagraFacile and provide users with more control over the initial data seeding, interactive setup scripts were needed for both macOS/Linux (`start.sh`) and Windows (`start.bat`). This aligns with the goal of creating a downloadable ZIP package with guided setup.
**Accomplishments:**
*   **Modified `start.sh` for Interactive Setup (macOS/Linux):**
    *   The `start.sh` script was significantly updated to be interactive.
    *   It now checks for `sagrafacile_config.json`, prompts for essential settings (Domain, Cloudflare Token, DB credentials, JWT Secret), asks for data seeding preferences, saves choices to `sagrafacile_config.json`, and generates the `.env` file.
*   **Created Interactive `start.bat` (Windows):**
    *   A new `start.bat` script was created, mirroring the interactive functionality of `start.sh` for Windows users. This includes configuration checking, prompting, data seeding choices, saving to `sagrafacile_config.json`, and `.env` file generation.
*   **Backend Data Seeding (`InitialDataSeeder.cs`):**
    *   The existing `InitialDataSeeder` service seamlessly integrates with the configurations provided by both `start.sh` and `start.bat` via the generated `.env` file, as it was already designed to read the necessary environment variables.
*   **Documentation Updates:**
    *   `README.md`: Updated installation instructions to reflect the new interactive `start.sh` and `start.bat` processes and the role of `sagrafacile_config.json`.
    *   `DEPLOYMENT_ARCHITECTURE.md`: Updated to detail the interactive script flow for both platforms, the `sagrafacile_config.json` file, and its relation to the `.env` file and backend seeding.
    *   `Roadmap.md`: Marked relevant tasks under "Phase 7: Deployment & Monitoring" as complete for both `start.sh` and `start.bat`, and related backend seeding logic.
**Key Decisions:**
*   Both `start.sh` and `start.bat` provide a consistent interactive setup experience across platforms.
*   `sagrafacile_config.json` serves as the central, persistent store for user-defined configurations.
*   The `.env` file is treated as a dynamically generated artifact.
*   The backend's `InitialDataSeeder` required no changes, demonstrating good foresight in its initial design.
**Outcome:** The setup process for SagraFacile is now significantly more user-friendly and flexible on both macOS/Linux and Windows. Users can easily configure essential settings and control initial data seeding through an interactive command-line interface.
**Next Steps:**
*   Continue with other pending tasks in "Phase 7: Deployment & Monitoring" of the `Roadmap.md`, such as defining the deployment ZIP package contents and creating the GitHub Actions workflow for release packaging.

## (2025-06-16) - Refactored Data Seeding Logic
**Context:** The initial data seeding logic (System Defaults, Demo Data, Initial Org/Admin from env vars) was previously located directly in `Program.cs`. This made `Program.cs` verbose and mixed startup configuration with data initialization.
**Accomplishments:**
*   **Created `InitialDataSeeder.cs`:**
    *   A new service `SagraFacile.NET.API.Data.InitialDataSeeder` (and `IInitialDataSeeder` interface) was created.
    *   This class now encapsulates all data seeding logic, including:
        *   `SeedSystemDefaultsAsync()`: Seeds "System" organization, default roles ("SuperAdmin", "Admin", "AreaAdmin", "Cashier", "Waiter"), and a SuperAdmin user (credentials configurable via `SUPERADMIN_EMAIL`, `SUPERADMIN_PASSWORD`).
        *   `SeedSagraDiTencarolaDataAsync()`: Seeds the "Sagra di Tencarola" demo data (organization, users, areas, menu categories, and items). Demo user password configurable via `DEMO_USER_PASSWORD`.
        *   `SeedInitialOrganizationAndAdminAsync()`: Seeds an initial organization and an "Admin" user based on environment variables (`INITIAL_ORGANIZATION_NAME`, `INITIAL_ADMIN_EMAIL`, `INITIAL_ADMIN_PASSWORD`). This runs only if `SAGRAFACILE_SEED_DEMO_DATA` is `false` (or not set) and no other user-defined organizations (besides "System" or "Sagra di Tencarola") exist.
    *   The main `SeedAsync()` method orchestrates these based on the `SAGRAFACILE_SEED_DEMO_DATA` environment variable (defaults to `false`).
    *   The seeder uses `IServiceScopeFactory` to correctly resolve `DbContext`, `UserManager`, and `RoleManager` within its scope.
    *   Includes a `GenerateSlug` utility for creating slugs for seeded organizations.
*   **Updated `Program.cs`:**
    *   Registered the new service: `builder.Services.AddScoped<IInitialDataSeeder, InitialDataSeeder>();`.
    *   Removed all previous inline data seeding blocks.
    *   Added a static extension method `SeedDatabaseAsync(this IApplicationBuilder app)` in `InitialDataSeeder.cs` which calls `IInitialDataSeeder.SeedAsync()`.
    *   This extension method is called in `Program.cs` after database migrations are applied and before `app.Run()`, ensuring it only runs when not in the "Testing" environment.
    *   Corrected logger instantiation in the static extension method to use `ILoggerFactory`.
**Key Decisions:**
*   Centralized all initial data seeding logic into a dedicated service for better organization and maintainability.
*   Made the seeding process conditional based on the `SAGRAFACILE_SEED_DEMO_DATA` environment variable.
*   Ensured that seeding of an initial organization/admin from environment variables is skipped if other user-defined organizations already exist, preventing accidental overwrites or duplicate setups.
*   SuperAdmin and Demo User passwords can be configured via environment variables for better security in production-like setups.
**Outcome:** `Program.cs` is now cleaner. Data seeding is more modular and easier to manage. The logic for choosing between demo data and initial admin setup is clearly defined.

---

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
*   **Key Decisions:** Used optimistic concurrency for state updates. `CallSpecificAsync` updates 'last called' but not `NextSequentialNumber`. Admin actions require `Admin`/`SuperAdmin`, Cashier actions allow `Cashier` role.
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

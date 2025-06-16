# SagraFacile API Backend

This project contains the backend API for the SagraFacile system, built with ASP.NET Core.

## Phase 1 Scope

This initial phase focuses on establishing the core backend functionality:

*   **Data Models:** Defining entities for Organizations, Areas, Users (using ASP.NET Identity), Roles, Menu Categories, Menu Items, Orders, and Order Items using Entity Framework Core.
*   **Database:** Setting up the `ApplicationDbContext` and configuring it for PostgreSQL (connected to `192.168.1.22:5432` by default in development).
*   **Identity:** Configured ASP.NET Core Identity for user and role management using the PostgreSQL database.
*   **Service Layer:** Implementing a basic service layer pattern to encapsulate business logic for CRUD operations on core entities.
*   **API Controllers:** Exposing RESTful endpoints for managing:
    *   Organizations (CRUD)
    *   Areas (CRUD, filterable by Organization)
    *   Menu Categories (CRUD, filterable by Area)
    *   Menu Items (CRUD, filterable by Category)
    *   Orders (Create, Get by ID, Get by Area)
    *   Accounts (Register, Login with JWT, Assign Role, Unassign Role, List Users, Get User, Update User, Delete User, List Roles, Create Role)
    *   Public (Get Org by Slug, Get Area by Slugs, Get Categories by Area, Get Items by Category, Create Pre-Order) - *New*
    *   Kitchen Display System (KDS) Configuration (Planned - See `../../KdsArchitecture.md`)
    *   Printer Configuration & Companion App Integration (Planned - See `../../PrinterArchitecture.md`)
    *   Waiter Mobile Interface Support (Planned - See `../../WaiterArchitecture.md`)
    *   Platform Synchronization (Menu Push, Pre-order Pull) - *New*
*   **Slug Support:** Added URL-friendly `Slug` property to `Organization` and `Area` models/DTOs, with automatic generation and unique constraints. - *New*
*   **Pre-Order Logic:** Added `PreOrder` status, customer details (`CustomerName`, `CustomerEmail`) to `Order` model, and service logic (`CreatePreOrderAsync`) for handling public pre-order submissions. `CashierId` made nullable. - *New*
*   **Email & QR Confirmation:** Implemented email sending (using `MailKit`) and QR code generation (using `QRCoder`) for pre-order confirmations. Emails include order details and an embedded QR code. - *New*
*   **Order Logic:** Includes calculation of total amount and custom order ID generation (`Utils/OrderIdGenerator.cs`).
*   **Authentication & Authorization:** JWT generation and validation configured. Role seeding on startup. Role-based authorization applied to controllers. Public endpoints configured with `[AllowAnonymous]`.
*   **Platform Synchronization:** - *New*
    *   **Sync Configuration:** Added `SyncConfiguration` model/service/controller for managing platform URL and API key per organization.
    *   **Menu Sync:** Implemented `MenuSyncService` to push Areas/Categories/Items to the platform API.
    *   **Pre-order Polling:** Implemented `PreOrderPollingService` and `PreOrderPollingBackgroundService` to periodically fetch new pre-orders from the platform API, import them locally (assigning `PreOrderPlatformId`), and mark them as fetched on the platform. Uses `IHttpClientFactory` and custom platform DTOs.
*   **Multi-Tenancy:** Organization-based access control implemented in services using JWT claims and `BaseService` helpers. SuperAdmins bypass these checks.
*   **Data Transfer Objects (DTOs):** Used consistently across controllers for API responses and specific inputs. Includes DTOs for platform interaction (`DTOs/Platform/`). See `DataStructures.md` for details.
*   **Integration Testing:** Setup using xUnit, `WebApplicationFactory`, and InMemory database. JWT authentication helpers created. Test suites added for core controllers. Seeding logic in `CustomWebApplicationFactory` updated. Tests refactored to use centralized `TestConstants.cs`. `IEmailService` mocked.

**Out of Scope for Phase 1:**

*   Frontend implementation (React/Next.js).
*   More robust order numbering mechanism (if needed beyond timestamp).
*   Complex workflows (printing, KDS, web orders, etc.).
*   Advanced features (inventory, complex discounts, statistics).

## Setup and Running

### Prerequisites

*   .NET SDK (Version compatible with the project, likely .NET 9 as per current setup)
*   PostgreSQL Instance (running at `192.168.1.22:5432` for development setup)
*   IDE like Visual Studio or VS Code
*   .NET EF Core tools (`dotnet tool install --global dotnet-ef`)
*   ASP.NET Core code generator tool (`dotnet tool install --global dotnet-aspnet-codegenerator`)

### Configuration

1.  **Connection String:** The `DefaultConnection` string in `SagraFacile.NET.API/appsettings.Development.json` is configured for the PostgreSQL instance (default: `Host=192.168.1.22;...`). Update this if your database details differ.
2.  **JWT Settings:** The `Jwt` section in `SagraFacile.NET.API/appsettings.json` contains the Issuer, Audience, and Key. **Crucially, replace the placeholder `Key` with a strong, secure secret key (at least 32 characters) and manage it securely (e.g., User Secrets, environment variables, Key Vault), not in source control.**
3.  **Email Settings:** The `EmailSettings` section in `SagraFacile.NET.API/appsettings.json` contains SMTP server details (Host, Port, UseSsl, SenderName, SenderEmail, Username, Password). Update these with your actual email provider's settings. For development/testing without a real SMTP server, consider using tools like `smtp4dev` or ensure the email service is mocked during tests. - *New*

### Database Migrations

Before running the application for the first time, you need to create and apply the database migrations.

**Using .NET CLI (Terminal):**

1.  Open a terminal/command prompt.
2.  Navigate to the API project directory:
    ```bash
    cd d:/Repos/SagraFacile/SagraFacile.NET/SagraFacile.NET.API
    ```
3.  Add a migration if you make changes to the models (the initial migration `InitialIdentitySetup` has already been created):
    ```bash
    dotnet ef migrations add YourMigrationName
    ```
4.  Apply the migration to the database:
    ```bash
    dotnet ef database update
    ```

**Using Visual Studio:**

1.  Open the solution (`SagraFacile.NET.sln`) in Visual Studio.
2.  Open the **Package Manager Console** (View -> Other Windows -> Package Manager Console).
3.  Ensure the **Default project** dropdown in the console is set to `SagraFacile.NET.API`.
4.  Add a migration if you make changes to the models:
    ```powershell
    Add-Migration YourMigrationName
    ```
5.  Apply the migration to the database:
    ```powershell
    Update-Database
    ```

### Running the API

**Using .NET CLI:**

1.  Navigate to the API project directory:
    ```bash
    cd d:/Repos/SagraFacile/SagraFacile.NET/SagraFacile.NET.API
    ```
2.  Run the application:
    ```bash
    dotnet run
    ```

**Using Visual Studio:**

1.  Set `SagraFacile.NET.API` as the startup project.
2.  Press `F5` or click the "Start Debugging" button (often looks like a green play icon).

The API will typically start on `https://localhost:xxxx` and `http://localhost:yyyy`, where `xxxx` and `yyyy` are ports defined in `SagraFacile.NET.API/Properties/launchSettings.json`.

## Running Integration Tests

The solution includes an integration test project (`SagraFacile.NET.API.Tests.Integration`).

**Using .NET CLI:**

1.  Navigate to the solution root directory:
    ```bash
    cd d:/Repos/SagraFacile/SagraFacile.NET
    ```
2.  Run the tests:
    ```bash
    dotnet test SagraFacile.NET.API.Tests.Integration/SagraFacile.NET.API.Tests.Integration.csproj
    ```
    *Or simply `dotnet test` from the solution root.*

**Using Visual Studio:**

1.  Open the **Test Explorer** (Test -> Test Explorer).
2.  Build the solution.
3.  Run tests from the Test Explorer window.

**Note:** The integration tests use an InMemory database provider and a custom `WebApplicationFactory` (`CustomWebApplicationFactory.cs`) to manage the test environment, including seeding test data (users, roles, organizations, areas, categories, items, orders, KDS stations, days, sync configurations) using centralized constants from `TestConstants.cs`. The seeding logic in the factory has been refined. Service update logic uses the fetch-update-save pattern. DTOs are used throughout the API (see `DataStructures.md`). The `IEmailService` is mocked using `Moq`. Background services like `PreOrderPollingBackgroundService` are typically not executed during standard integration tests unless specifically configured or tested separately.

**Current Test Status (2025-04-29 15:07):**
*   The `BasicIntegrationTests` suite (covering `OrganizationsController`) is **passing** (14/14 tests).
*   The `AreaIntegrationTests` suite is **passing** (17/17 tests).
*   The `MenuCategoryIntegrationTests` suite is **passing** (covering GET, POST, PUT, DELETE - 26/26 tests).
*   The `MenuItemIntegrationTests` suite is **passing** (covering GET, POST, PUT, DELETE - 26/26 tests).
*   The `OrderIntegrationTests` suite is **passing** (covering POST, GET - 15/15 tests).
*   The `AccountIntegrationTests` suite is **passing** (35/35 tests).
*   The `PublicControllerIntegrationTests` suite is **passing** (21/21 tests).
*   The `KdsStationIntegrationTests` suite is **passing** (20/20 tests).
*   The `DayIntegrationTests` suite is **passing** (23/23 tests).
*   The `SyncControllerIntegrationTests` suite needs to be created.
*   *(Note: Includes 1 default test in `UnitTest1.cs`)*
*   **Total Passing:** ???/??? tests. - *Needs Update*

## Testing the API Manually

Once the API is running, you can test the endpoints using tools like:

*   **Swagger UI:** If running in Development mode, navigate to `/openapi` (e.g., `https://localhost:xxxx/openapi`) in your browser for an interactive API documentation and testing interface.
*   **Postman / Insomnia:** Send HTTP requests (GET, POST, PUT, DELETE) to the API endpoints (e.g., `https://localhost:xxxx/api/organizations`). Remember to set the `Content-Type` header to `application/json` for POST/PUT requests with a body.

**Example Requests:**

*   `GET /api/organizations`
*   `POST /api/areas` with JSON body: `{ "name": "Stand Gastronomico", "organizationId": 1 }`
*   `GET /api/menucategories?areaId=1`
*   `POST /api/orders` with JSON body (refer to `CreateOrderDto` structure in `SagraFacile/Services/Interfaces/IOrderService.cs`)
*   `POST /api/accounts/register` with JSON body: `{ "email": "test@sagrafacile.it", "password": "Password123!", "confirmPassword": "Password123!", "firstName": "Test", "lastName": "User" }`
*   `POST /api/accounts/login` with JSON body: `{ "email": "test@sagrafacile.it", "password": "Password123!" }` (Returns JWT on success)
*   `GET /api/accounts` (Requires "Admin" or "SuperAdmin" role and JWT)
*   `PUT /api/accounts/{userId}` (Requires appropriate Admin role and JWT) with JSON body: `{ "firstName": "Updated", "lastName": "Name", "email": "updated@sagrafacile.it" }`
*   `DELETE /api/accounts/{userId}` (Requires appropriate Admin role and JWT)
*   `GET /api/accounts/roles` (Requires Admin role and JWT)
*   `POST /api/accounts/roles` (Requires "SuperAdmin" role and JWT) with JSON body: `{ "name": "NewRoleName" }`
*   `POST /api/accounts/assign-role` (Requires "SuperAdmin" role and JWT) with JSON body: `{ "userId": "user-guid-string", "roleName": "Cashier" }`
*   `GET /api/public/organizations/your-org-slug`
*   `GET /api/public/organizations/your-org-slug/areas/your-area-slug`
*   `GET /api/public/areas/1/menucategories`
*   `GET /api/public/menucategories/1/menuitems`
*   `POST /api/public/preorders` with JSON body (refer to `PreOrderDto` structure in `DataStructures.md`)

**Note:** ASP.NET Core Identity, JWT authentication/authorization, and role seeding are configured. Role-based authorization is applied to controllers. Login returns a JWT containing user claims (ID, email, name, organization ID, roles). Multi-tenancy checks (ensuring users only access data within their assigned organization) have been implemented across relevant services (`AccountService`, `AreaService`, `MenuCategoryService`, `MenuItemService`, `OrderService`). Remember to provide the JWT as a Bearer token in the `Authorization` header for secured endpoints. Public endpoints under `/api/public` do not require authentication.

# SagraFacile - Open-Source Festival & Food Event Management System

SagraFacile is an open-source, self-hosted system designed for managing food festivals, sagre, and similar gastronomic events. It provides a flexible and comprehensive solution for organizations like pro loco associations to handle everything from menu management and ordering to kitchen displays and reporting.

## Project Goal

To create a robust, easy-to-deploy, and customizable POS and event management system that can be run entirely on local infrastructure.

## Architecture Overview

The system consists of two main components:

1.  **Backend (.NET API):**
    *   Built with ASP.NET Core (.NET 9).
    *   Provides RESTful API endpoints for managing:
        *   Organizations & Areas (Stands)
        *   Menus (Categories, Items, Variants)
        *   Orders (Creation, Payment, Status Tracking, Pre-order Import)
        *   Users & Authentication (JWT-based)
        *   Roles & Permissions
        *   Printers & KDS Configuration
        *   SagraPreOrdine Platform Synchronization (Menu Push, Pre-order Pull)
        *   Stock Levels (Scorta)
        *   Display Order Numbers (human-readable, day/area-specific)
    *   Uses SignalR for real-time communication (KDS, Public Displays).
    *   Designed for multi-tenancy (data isolation per organization).
    *   Uses PostgreSQL as the database via Entity Framework Core.
    *   Located in the `SagraFacile.NET/` directory. See `SagraFacile.NET/README.md` for backend-specific details.

2.  **Frontend (Next.js App):**
    *   Built with Next.js (v14+ App Router) and TypeScript.
    *   Single application serving both public-facing and internal interfaces.
    *   **Styling:** Tailwind CSS with Shadcn/ui components.
    *   **State Management:** React Context API (initially, potentially adding Zustand for more complex state).
    *   **API Client:** Axios or Fetch wrapper for interacting with the .NET backend.
    *   **Real-time:** SignalR client integration.
    *   **QR Codes:** Reading capabilities (Waiter UI) and displaying backend-generated codes (Cashier UI).
    *   Located in the `sagrafacile-webapp/` directory.

## Printing Architecture

To handle silent receipt printing from cashier stations and flexible comanda printing to various kitchen/bar printers (network or USB via dedicated PCs), SagraFacile employs a hybrid approach:

1.  **Windows Companion App (`SagraFacile.WindowsPrinterService`):** A lightweight .NET WinForms application (targeting .NET 9+) installed on Windows PCs with connected USB printers (cashier stations, specific kitchen stations). It runs as a background/tray application, listens for print jobs via local WebSockets (for receipts/local comandas) or SignalR (for remote comandas pushed by the backend), and prints silently using native Windows APIs.
2.  **Backend Print Service:** The .NET API includes a `PrintService` that manages printer configurations (stored in the database) and orchestrates printing. It can send jobs directly to network printers (via TCP/IP) or trigger specific Companion Apps via SignalR for USB printers.

Configuration allows assigning menu categories to printers and defining whether comandas print locally at the cashier or are dispatched by the backend.

See `PrinterArchitecture.md` for detailed diagrams and implementation phases.

See `WaiterArchitecture.md` for details on the mobile interface for waiters.

## Kitchen Display System (KDS) Architecture

SagraFacile includes a Kitchen Display System designed to act as a final checklist for preparers before handing off trays.

*   **Purpose:** Ensure all items assigned to a specific preparation station (e.g., Kitchen, Bar) for an order are accounted for on the tray.
*   **Workflow:** KDS screens display relevant pending items for their assigned categories. Preparers select an order, tap items on the screen to confirm they are on the physical tray, and close the view. Orders disappear from the KDS once all their items for that station are confirmed.
*   **Technology:** Relies on SignalR for real-time updates and backend logic to track item confirmation status (`KdsStatus`) per station. The overall order status (`ReadyForPickup`) is updated automatically when all items across all stations are confirmed.

See `KdsArchitecture.md` for the detailed workflow, technical components, and diagrams.

## SagraPreOrdine Platform Synchronization

SagraFacile can integrate with the external SagraPreOrdine companion platform:
*   **Menu Sync:** Push local menu definitions (Areas, Categories, Items) to the platform.
*   **Pre-order Polling:** Automatically pull new pre-orders placed on the platform into the local SagraFacile system.

This requires configuring API keys and base URLs within the SagraFacile Admin interface.

## Frontend Interfaces

The Next.js application will include the following key interfaces:

*   **Public:**
    *   `/` (Optional Landing Page)
    *   `/preordine`: Customer pre-order interface (backend sends confirmation email with QR code).
    *   `/qdisplay/org/{orgSlug}/area/[areaId]`: Real-time customer queue display screen showing called ticket numbers and assigned cashier stations, with audio announcements.
    *   `/pickup-display/org/{orgSlug}/area/[areaId]`: Real-time display of orders ready for pickup, with audio announcements.
*   **Internal (requires login, accessed via `/app/*`):**
    *   `/app/login`: Staff login page.
    *   `/app/cassa`: Cashier interface for order taking, payment, displaying order QR code, receipt printing, customer queue system controls, and stock level display.
    *   `/app/cameriere`: Waiter interface for scanning order QR codes and assigning tables.
    *   `/app/kds/{zona}`: Kitchen Display System showing orders for a specific zone.
    *   `/app/app/org/[orgId]/area/[areaId]/pickup-confirmation/page.tsx`: Staff interface to confirm order pickups.
    *   `/app/admin`: Administration panel for managing Menus, Areas, Users, Printers, KDS, Stock Management (Scorta), and viewing Reports.

## Docker Deployment

This project is designed to be deployed using Docker Compose, which simplifies the setup of the entire application stack (backend, frontend, database, and reverse proxy) for a local, on-premise network.

This setup uses the **Caddy web server** as a reverse proxy to provide **automatic HTTPS** for the internal network using self-signed certificates.

### Prerequisites

*   [Docker](https://docs.docker.com/get-docker/) installed and running on your host machine.
*   [Docker Compose](https://docs.docker.com/compose/install/) (usually included with Docker Desktop).

### How to Run the Application

1.  **Clone the repository** if you haven't already.

2.  **Start the services** by running the following command from the root of the project directory:
    ```bash
    docker-compose up --build -d
    ```
    *   `--build` tells Docker Compose to build the images from the `Dockerfile`s the first time it's run or if the source code has changed.
    *   `-d` runs the containers in detached mode (in the background).

3.  **Access the Application:**
    *   Find the local IP address of the machine running Docker (e.g., `192.168.1.100`).
    *   Open a web browser and navigate to `https://<your-local-ip-address>`.

4.  **Trust the Self-Signed Certificate:**
    *   The first time you connect, your browser will show a security warning (e.g., "Your connection is not private"). This is expected because the HTTPS certificate is self-signed by Caddy and is not recognized by a public Certificate Authority.
    *   You must manually instruct your browser to trust this certificate. Look for an "Advanced" or "Details" button and then an option like "Proceed to [IP address] (unsafe)".
    *   For a more permanent solution, you can install Caddy's root certificate (`caddy_data/_data/caddy/pki/authorities/local/root.crt` inside the Docker volume) on your client machines.

### Services Overview

The `docker-compose.yml` file defines the following services:

*   **`db`**: The PostgreSQL database where all data is stored.
*   **`backend`**: The .NET API service.
*   **`frontend`**: The Next.js web application.
*   **`caddy`**: The reverse proxy that handles all incoming traffic.
    *   It exposes ports `80` and `443` to the host network.
    *   Redirects all HTTP traffic to HTTPS.
    *   Routes requests to `/api/*` to the **backend** service.
    *   Routes all other requests to the **frontend** service.

### Stopping the Application

To stop all the running services, use the command:
```bash
docker-compose down
```
To stop and remove the data volumes (deleting all database data), use:
```bash
docker-compose down -v
```

## Contributing

(Contribution guidelines to be added later).

## License

This project is licensed under the [MIT License](LICENSE.txt).

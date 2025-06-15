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

## Docker Deployment & Installation Guide

SagraFacile is designed for easy local deployment using Docker and Docker Compose. This setup packages the backend API, frontend web application, PostgreSQL database, and a Caddy reverse proxy (for automatic HTTPS) into a cohesive system.

### 1. Prerequisites

*   **Docker:** Install Docker Desktop (for Windows or macOS) or Docker Engine (for Linux).
    *   Windows: [Get Docker Desktop for Windows](https://docs.docker.com/desktop/install/windows-install/)
    *   macOS: [Get Docker Desktop for Mac](https://docs.docker.com/desktop/install/mac-install/)
    *   Linux: [Install Docker Engine](https://docs.docker.com/engine/install/) (select your distribution)
*   **Docker Compose:** Usually included with Docker Desktop. For Linux, if not included, follow the [Docker Compose installation guide](https://docs.docker.com/compose/install/).
*   **Text Editor:** A simple text editor (like Notepad++, VS Code, Sublime Text, Nano, Vim) for editing configuration files.
*   **Internet Connection:** Required for the initial download of Docker images.

### 2. Network Configuration (Important!)

A stable network setup is crucial for SagraFacile to operate correctly, especially in a multi-device environment like a food festival.

*   **Static IP for the Server:** The computer running SagraFacile (the Docker host) **must have a static IP address** on your local network (e.g., `192.168.1.10`). This ensures that all client devices (cashier PCs, waiter phones, KDS screens) can reliably connect to the server.
*   **DHCP Range:** Configure your main network router to assign dynamic IP addresses (DHCP) in a range that does not conflict with your chosen static IPs (e.g., DHCP range `192.168.1.50` to `192.168.1.200`).
*   **Router IP:** Note your router's IP address (e.g., `192.168.1.1`), as this will be the gateway for your server.

For detailed guidance on network planning, component recommendations, and IP addressing strategies, please refer to **`docs/NetworkingArchitecture.md`** included in this package.

### 3. Installation Steps

1.  **Download SagraFacile:**
    *   Download the latest `SagraFacile-vX.Y.Z.zip` package from the [GitHub Releases page](https://github.com/your-username/sagrafacile/releases) (Replace with the actual repository URL).
    *   Extract the ZIP file to a folder on your computer (e.g., `C:\SagraFacile` or `/home/user/SagraFacile`).

2.  **Configure Environment Variables:**
    *   Navigate to the directory where you extracted SagraFacile.
    *   Copy the `.env.example` file and rename the copy to `.env`.
    *   Open `.env` with your text editor. This is a critical step.
    *   Carefully review all settings and **you MUST change placeholder values**, especially for:
        *   `POSTGRES_PASSWORD`
        *   `JWT_SECRET` (make this a very long, random string for security)
        *   `INITIAL_ADMIN_EMAIL` (if you want an initial admin user created)
        *   `INITIAL_ADMIN_PASSWORD` (if you want an initial admin user created)
    *   Save the `.env` file.

3.  **Start SagraFacile:**
    *   **For Windows:**
        *   Double-click `start.bat`.
    *   **For macOS or Linux:**
        *   Open a terminal in the SagraFacile directory.
        *   Make the scripts executable (only needs to be done once):
            ```bash
            chmod +x start.sh
            chmod +x update.sh
            chmod +x stop.sh
            ```
        *   Run the start script: `./start.sh`
    *   This command will pull the necessary Docker images (if not already present) and start all SagraFacile services in the background.
    *   This process might take a few minutes on the first run as Docker downloads the images. Subsequent starts will be much faster.
    *   The script will provide you with the URL to access the application.

5.  **MANDATORY - Trust the Self-Signed HTTPS Certificate:**
    SagraFacile uses the Caddy web server to automatically provide HTTPS for your local network using a self-signed certificate. For your browsers to trust this local HTTPS connection, you **must** install Caddy's root CA certificate on:
    *   The computer running SagraFacile (the Docker host).
    *   **ALL client devices** (cashier PCs, waiter phones, tablets, etc.) that will access SagraFacile.

    The `start.bat` or `start.sh` script (or this README) provides platform-specific instructions. Here's a summary:

    *   **General Steps:**
        1.  **Copy the certificate from the Caddy container:**
            ```bash
            docker cp sagrafacile_caddy:/data/caddy/pki/authorities/local/root.crt .
            ```
            (This command should be run in a terminal/command prompt on the machine where Docker is running, from the SagraFacile directory).
        2.  **Install `root.crt` into your system's trust store:**
            *   **Windows (Administrator Command Prompt/PowerShell):**
                ```powershell
                certutil -addstore -f "ROOT" "root.crt"
                ```
            *   **macOS (Terminal):**
                ```bash
                sudo security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain root.crt
                ```
            *   **Linux (Debian/Ubuntu based - Terminal):**
                ```bash
                sudo mkdir -p /usr/local/share/ca-certificates/extra
                sudo cp root.crt /usr/local/share/ca-certificates/extra/sagrafacile-local-root.crt
                sudo update-ca-certificates
                ```
                (For other Linux distributions, consult your system's documentation.)
        3.  **(Optional) Delete the copied `root.crt` file** after successful installation.
        4.  **Restart your web browser(s)** after installing the certificate.

    *   **Connecting Other Devices:** For each additional device that needs to access SagraFacile:
        1.  Transfer the `root.crt` file (that you copied from the Caddy container) to the device.
        2.  Install it according to the device's operating system instructions (e.g., on Android or iOS, you typically open the certificate file and follow prompts to install it).

### 4. Accessing SagraFacile

*   Once the services are running and the CA certificate is trusted:
    *   **On the server machine:** Open your web browser and go to `https://localhost`
    *   **From other devices on the network:** Open your web browser and go to `https://<server-ip-address>` (e.g., `https://192.168.1.10`, using the static IP you assigned to the server).

    You should see the SagraFacile login page or public interface.

### 5. Managing SagraFacile Services

Use the provided scripts in the SagraFacile root directory:

*   **To Start SagraFacile:**
    *   Windows: Double-click `start.bat`
    *   macOS/Linux: Open a terminal and run `./start.sh`
*   **To Stop SagraFacile:**
    *   Windows: Double-click `stop.bat`
    *   macOS/Linux: Open a terminal and run `./stop.sh`
    *   This will stop all running SagraFacile services.
*   **To Update SagraFacile:**
    *   When a new version is announced, download the latest package if it contains updated `docker-compose.yml`, scripts, or other configuration files. Otherwise, simply run the update script.
    *   Windows: Double-click `update.bat`
    *   macOS/Linux: Open a terminal and run `./update.sh`
    *   This will pull the latest Docker images for the backend and frontend and restart the services. Your data in the database will be preserved.
*   **To View Logs:**
    *   Open a terminal in the SagraFacile directory and run:
        ```bash
        docker-compose logs -f
        ```
    *   To view logs for a specific service (e.g., `backend`, `frontend`, `caddy`, `db`):
        ```bash
        docker-compose logs -f backend
        ```
*   **To Stop Services and Remove Data Volumes (WARNING: DELETES ALL DATABASE DATA AND CADDY CERTIFICATES):**
    *   Open a terminal in the SagraFacile directory and run:
        ```bash
        docker-compose down -v
        ```
    *   **Use this command with extreme caution as it will permanently delete all your event data.**

### 6. Windows Printer Service (Companion App)

For printing receipts and comandas to USB-connected printers on Windows machines:

1.  Locate the `SagraFacile.WindowsPrinterService.Setup.exe` installer (this will be part of the final distribution package).
2.  Run the installer on each Windows PC that has a USB printer to be used with SagraFacile.
3.  During installation, or by editing its settings post-installation, configure the companion app:
    *   **Server Base URL:** Point it to your SagraFacile server's address using your domain (e.g., `https://pos.my-restaurant-pos.com`).
    *   **Instance GUID:** Generate a unique GUID for each printer instance.
    *   **Selected Printer:** Choose the correct Windows printer from the dropdown.
4.  Ensure the Windows PC can access the SagraFacile server URL (e.g., `https://pos.my-restaurant-pos.com`). The Let's Encrypt certificate will be trusted automatically by Windows if the PC has internet access.
5.  In the SagraFacile Admin interface, configure these printers using their GUIDs.

See `docs/PrinterArchitecture.md` for more details.

### 7. Services Overview (Docker Compose)

The `docker-compose.yml` file defines and configures the following services:

*   **`db`**: The PostgreSQL database (version 15) where all SagraFacile data is stored. Data is persisted in a Docker volume.
*   **`backend`**: The .NET API service (SagraFacile.NET.API). This image is pulled from a container registry (e.g., Docker Hub). It connects to the `db` service and exposes an internal HTTP port (8080), which Caddy proxies.
*   **`frontend`**: The Next.js web application (sagrafacile-webapp). This image is pulled from a container registry. It communicates with the `backend` service (via Caddy) and exposes an internal HTTP port (3000), which Caddy proxies.
*   **`caddy`**: The Caddy web server (version 2) acting as a reverse proxy. This image is pulled from Docker Hub.
    *   Exposes ports `80` (HTTP) and `443` (HTTPS) to your host network.
    *   Redirects all HTTP traffic to HTTPS.
    *   Provides automatic HTTPS using self-signed certificates via its `local_certs` feature.
    *   Routes requests to `/api/*` to the `backend:8080` service.
    *   Routes all other requests to the `frontend:3000` service.
    *   Caddy's data (including generated certificates) is persisted in Docker volumes.

### 8. Troubleshooting

*   **Cannot access `https://localhost` or `https://<server-ip>`:**
    *   Ensure Docker services are running: `docker-compose ps`
    *   Verify the CA certificate is installed correctly on the machine you are testing from, and that your browser has been restarted.
    *   Check firewall settings on the server machine; ensure incoming connections on port 443 are allowed.
    *   Check Caddy logs: `docker-compose logs -f caddy`
*   **API errors or frontend not loading data:**
    *   Check backend logs: `docker-compose logs -f backend`
    *   Check frontend logs: `docker-compose logs -f frontend`
    *   Ensure the `NEXT_PUBLIC_API_BASE_URL=/api` is correctly set for the frontend service in `docker-compose.yml` and that the frontend is making calls to relative paths like `/api/orders`.
*   **Database connection issues (from backend logs):**
    *   Verify `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB` in your `.env` file match what the backend expects for its connection string. The `docker-compose.yml` directly constructs the connection string for the backend using these.

## Contributing

(Contribution guidelines to be added later).

## License

This project is licensed under the [MIT License](LICENSE.txt).

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

SagraFacile uses Caddy with Let's Encrypt (via Cloudflare DNS challenge) for automatic HTTPS using your domain name. This provides a trusted SSL certificate for all devices on your local network.

*   **Server's Local IP Address:** The computer running SagraFacile (the Docker host) should ideally have a **static local IP address** (e.g., `192.168.1.10`). This makes local network access consistent.
*   **Public IP Address & Domain:** Your internet connection has a public IP address. Your domain name (e.g., `pos.myrestaurant.com`), managed by Cloudflare, will need an A record pointing to this public IP. This is primarily for Caddy to interact with Cloudflare and for Let's Encrypt to verify domain ownership via DNS records.
*   **Port Forwarding (Optional - Only for External Access):**
    *   If you **only** want to access SagraFacile from **within your local network**, you **do not need to set up port forwarding** on your router for ports 80 and 443. The Let's Encrypt DNS-01 challenge (used with Cloudflare) does not require your server to be directly accessible from the internet for certificate acquisition.
    *   If you **do** want to access SagraFacile from the internet (outside your local network), then you would need to configure your router to forward incoming traffic on ports 80 (HTTP) and 443 (HTTPS) to the local IP address of the server running SagraFacile. **This guide assumes local network access only by default.**
*   **Local DNS Override (Essential for Local Network Access with Domain Name):**
    For devices *inside your local network* to use `https://your.domain.com` (the `MY_DOMAIN` you'll set) and reach your local SagraFacile server directly, you **must** configure your local network router's "Local DNS" (sometimes called "Static Hostname," "DNSMasq," or similar) feature.
    *   Map `your.domain.com` (e.g., `pos.myrestaurant.com`) to the SagraFacile server's **local IP address** (e.g., `192.168.1.10`).
    *   This ensures local devices resolve the domain to the local server, not to its public IP via the internet.

For detailed guidance on network planning, see **`docs/NetworkingArchitecture.md`**.

### 3. Installation Steps

1.  **Download SagraFacile:**
    *   Download the latest `SagraFacile-vX.Y.Z-dist.zip` package from the [GitHub Releases page](https://github.com/your-username/sagrafacile/releases) (Replace with the actual repository URL).
    *   Extract the ZIP file to a folder on your computer (e.g., `C:\SagraFacile` or `/home/user/SagraFacile`).

2.  **Domain & Cloudflare Setup (Prerequisites for the script):**
    *   **Domain in Cloudflare:** Ensure your registered domain name is added to your Cloudflare account, and its nameservers are pointing to Cloudflare.
    *   **Cloudflare API Token:** Create a Cloudflare API Token:
        *   In Cloudflare: "My Profile" -> "API Tokens" -> "Create Token".
        *   Use the "Edit zone DNS" template.
        *   Permissions: `Zone:DNS:Edit`.
        *   Zone Resources: Select your specific domain.
        *   **Copy the generated token securely.** You'll need it for the setup script.
    *   **DNS A Record in Cloudflare:** Ensure an A record (e.g., for `pos.yourdomain.com` or `yourdomain.com`) points to your network's current **public IP address**. Caddy uses this information during its interaction with Cloudflare for the DNS challenge, even if the server itself isn't publicly exposed via port forwarding for this purpose.

3.  **Run Interactive Setup Script:**
    *   Navigate to the extracted SagraFacile folder.
    *   **Windows:** Double-click `start.bat`. (Note: `start.bat` will be updated to be interactive in a future step).
    *   **macOS/Linux:**
        *   Open a terminal in the SagraFacile folder.
        *   Make scripts executable (if you haven't already): `chmod +x *.sh`
        *   Run: `./start.sh`
    *   **Follow Prompts:** The `start.sh` script is now interactive:
        *   It will check for an existing `sagrafacile_config.json`. If found, it will ask if you want to use it, re-configure, or exit.
        *   If no config exists or you choose to re-configure, it will guide you to enter:
            *   Your full domain name (e.g., `pos.yourdomain.com`). This becomes `MY_DOMAIN`.
            *   Your Cloudflare API Token.
            *   Database credentials (user, password, DB name).
            *   A secure JWT Secret (with an option to auto-generate).
            *   Your preference for initial data: Seed demo data OR set up an initial organization/admin (prompts for organization name, admin email, and admin password if this option is chosen).
    *   The script saves your settings to `sagrafacile_config.json` and then generates the `.env` file based on this configuration.
    *   Services start via `docker-compose up -d`. Caddy will then attempt to obtain the Let's Encrypt certificate using the Cloudflare DNS challenge. This may take a few moments. Check Caddy logs (`docker-compose logs -f caddy`) for progress.

### 4. Accessing SagraFacile (Local Network):
    *   Once services are running and Caddy has successfully obtained the certificate:
        *   Ensure your **Local DNS Override** (Step 2, last bullet point under Network Configuration) is correctly configured on your router.
        *   From any device on your local network, open a web browser and go to `https://your.domain.com` (using the `MY_DOMAIN` you configured).
    *   You should see the SagraFacile login page or public interface. Because Caddy obtains a publicly trusted Let's Encrypt certificate, **no client-side certificate installation is needed.**

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

The `docker-compose.yml` file defines and configures these services. The interactive `start.sh` script (and eventually `start.bat`) first collects your configuration choices into `sagrafacile_config.json`, and then uses this file to generate the necessary `.env` file that `docker-compose.yml` relies on.

*   **`db`**: The PostgreSQL database (version 15) where all SagraFacile data is stored. Data is persisted in a Docker volume.
*   **`api`** (formerly `backend`): The .NET API service (SagraFacile.NET.API). This image is pulled from a container registry. It connects to the `db` service and exposes an internal HTTP port (8080), which Caddy proxies.
*   **`frontend`**: The Next.js web application (sagrafacile-webapp). This image is pulled from a container registry. It communicates with the `api` service (via Caddy) and exposes an internal HTTP port (3000), which Caddy proxies.
*   **`caddy`**: The Caddy web server (version 2) acting as a reverse proxy. This image is pulled from Docker Hub.
    *   Exposes ports `80` (HTTP) and `443` (HTTPS) to your host network.
    *   Redirects all HTTP traffic to HTTPS.
    *   Provides automatic HTTPS using **Let's Encrypt** with the Cloudflare DNS challenge, configured via your `MY_DOMAIN` and `CLOUDFLARE_API_TOKEN` from the `.env` file.
    *   Routes requests to `/api/*` to the `api:8080` service.
    *   Routes all other requests to the `frontend:3000` service.
    *   Caddy's data (including obtained Let's Encrypt certificates) is persisted in Docker volumes.

### 8. Troubleshooting

*   **Cannot access `https://your.domain.com`:**
    *   **Docker Services:** Check `docker-compose ps`. Are all services (`api`, `frontend`, `caddy`, `db`) running?
    *   **Caddy Logs (Crucial):** `docker-compose logs -f caddy`. Look for errors related to:
        *   **Certificate Acquisition:** "obtaining certificate", "presenting DNS-01 challenge", "waiting for propagation", "failed to get certificate".
            *   Verify `MY_DOMAIN` and `CLOUDFLARE_API_TOKEN` in `sagrafacile_config.json` are correct. The `.env` file is generated from this, so ensure the source config is accurate.
            *   Ensure the Cloudflare API Token has `Zone:DNS:Edit` permissions for your domain in Cloudflare.
            *   Verify your domain's A record in Cloudflare DNS points to your network's current **public IP address**. This is needed for the DNS-01 challenge mechanism.
            *   If you intend **local network access only**, ensure **port forwarding is NOT active** for ports 80/443 on your router from the internet to your server. If it is active, and you only want local access, remove it.
        *   **Proxying:** Errors like "no such host" if Caddy can't reach `api` or `frontend` services (internal Docker networking issue).
    *   **Local DNS Override:** If accessing from *within* your local network, ensure your router's Local DNS setting correctly maps `your.domain.com` to the SagraFacile server's **local IP**. This is the primary way local devices will find your server.
    *   **Firewall:** Ensure your server's firewall (if any) allows incoming connections to Docker/Caddy on the necessary ports if you *were* using port forwarding. For local-only access, this is less likely to be an issue for Caddy's internal operations but good to keep in mind.
    *   **API errors or frontend not loading data:**
    *   API service logs: `docker-compose logs -f api`
    *   Frontend logs: `docker-compose logs -f frontend`
    *   Ensure the `NEXT_PUBLIC_API_BASE_URL=/api` is correctly set in the generated `.env` file (sourced from `sagrafacile_config.json`).
*   **Database connection issues (from `api` service logs):**
    *   Verify `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB` in `sagrafacile_config.json` are correct. The `.env` file, which the API service uses for its connection string, is generated from this configuration file.


## Contributing

(Contribution guidelines to be added later).

## License

This project is licensed under the [MIT License](LICENSE.txt).

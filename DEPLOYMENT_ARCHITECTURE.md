# SagraFacile Deployment Architecture

## 1. Introduction

This document outlines the deployment architecture for SagraFacile. The primary goal is to provide a clear, actionable plan for a "Guided Manual Setup" approach, enabling a medium-technical user to deploy the SagraFacile application stack on a Windows machine using Docker.

Considerations for deploying on macOS and Linux systems with Docker are also included, primarily focusing on alternative helper scripts and certificate installation methods.

The deployment strategy focuses on distributing pre-built Docker images for the SagraFacile backend (.NET API) and frontend (Next.js webapp). Users will download a package containing a `docker-compose.yml` file, helper scripts, and configuration templates. The `docker-compose.yml` will pull these pre-built images from a container registry (e.g., Docker Hub, GitHub Container Registry) along with official images for PostgreSQL and Caddy.
This approach simplifies the user's setup significantly by removing the need for local builds.

## 2. Core Technologies

*   **Docker:** For containerizing individual application components (backend, frontend, database).
*   **Docker Compose:** For defining and orchestrating the multi-container application stack.
*   **Caddy Web Server:** As a reverse proxy to manage incoming traffic, provide automatic HTTPS using **Let's Encrypt with the DNS-01 challenge (via Cloudflare)**, and route requests to the appropriate services.
*   **Cloudflare:** For DNS management to facilitate the Let's Encrypt DNS-01 challenge.
*   **Registered Domain Name:** A publicly registered domain name is required.

## 3. Overall Architecture (Conceptual Flow)

```
End User Browser (HTTPS)
       |
       v
+-----------------+
End User Browser (HTTPS) --> your.domain.com
       |
       v
Internet DNS (Cloudflare) resolves your.domain.com to your Public IP
       |
       v
Your Router (Port Forwarding 80 & 443 to Server's Local IP)
       |
       v
+-----------------+
| Caddy           | (Reverse Proxy, SSL Termination with Let's Encrypt)
| (Container)     |
| Server Local IP |
| :80, :443       |
+-----------------+
       |        \
       |         \ (routes /api/*)
       v          v
+-----------------+     +-----------------+
| Frontend        |     | Backend (.NET)  |
| (Next.js App)   |     | (API Service)   |
| (Container)     |     | (Container)     |
| :3000           |     | :8080           |
+-----------------+     +-----------------+
                            |
                            v
                        +-----------------+
                        | PostgreSQL DB   |
                        | (Container)     |
                        | :5432           |
                        +-----------------+

Local Network Device Browser (HTTPS) --> your.domain.com
       |
       v
Your Router (Local DNS Resolution: your.domain.com to Server's Local IP)
       |
       v
+-----------------+
| Caddy           | (Same Caddy container as above)
| (Container)     |
| Server Local IP |
| :80, :443       |
+-----------------+
 (Traffic flows as above)
```

## 4. Phased Implementation Plan

The deployment process is broken down into five phases:

### Phase 1: Code & Project Preparation

Ensure applications are configurable via environment variables.

**Task 1.1: Backend (.NET API - `SagraFacile.NET.API`) Configuration**
*   **Connection String:** Modify `appsettings.json` to read the PostgreSQL connection string from an environment variable (e.g., `CONNECTION_STRING`).
*   **JWT Configuration:** Ensure JWT secret key, issuer, and audience are read from environment variables (e.g., `JWT_SECRET`, `JWT_ISSUER`, `JWT_AUDIENCE`).
*   **Database Migrations:** Configure `Program.cs` to automatically apply Entity Framework Core migrations on startup.
    ```csharp
    // In Program.cs, after builder.Build();
    var app = builder.Build();
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var dbContext = services.GetRequiredService<ApplicationDbContext>(); // Replace ApplicationDbContext with your actual DbContext
            dbContext.Database.Migrate();
        }
        catch (Exception ex)
        {
            var logger = services.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred while migrating the database.");
            // Optionally, rethrow or handle as appropriate for your application startup
        }
    }
    // ... rest of Program.cs
    ```
*   **Initial Admin User (Optional):** Implement logic to create an initial admin user from environment variables (e.g., `INITIAL_ADMIN_EMAIL`, `INITIAL_ADMIN_PASSWORD`, `INITIAL_ORGANIZATION_NAME`) if no admin users exist and if `SAGRAFACILE_SEED_DEMO_DATA` is `false` or not set.
*   **Demo Data Seeding Control:** The API should check for an environment variable `SAGRAFACILE_SEED_DEMO_DATA`. If `true`, it seeds the standard demo data. If `false` or not present, it proceeds with the initial admin/organization setup based on the other `INITIAL_` variables.

**Task 1.2: Frontend (Next.js - `sagrafacile-webapp`) Configuration**
*   **API URL:** Ensure API calls use a relative URL. In Next.js environment configuration (e.g., `.env.local`, `next.config.js`), set `NEXT_PUBLIC_API_BASE_URL=/api`. The API client will then make calls like `${process.env.NEXT_PUBLIC_API_BASE_URL}/orders`.

---

### Phase 2: Docker Image Creation & Publishing (Developer Responsibility)

This phase is now primarily a developer/CI-CD responsibility, not part of the end-user setup.

**Task 2.1: Create/Update Backend Dockerfile (`SagraFacile.NET/SagraFacile.NET.API/Dockerfile`)**
*   (Content remains the same as before, defining how the image is built by the developer/CI.)

**Task 2.2: Create Frontend Dockerfile (`sagrafacile-webapp/Dockerfile`)**
*   (Content remains the same as before, defining how the image is built by the developer/CI.)

**Task 2.3: Build and Push Images to Container Registry (Developer/CI)**
*   The developer (or a CI/CD pipeline) will build the Docker images using the Dockerfiles from Task 2.1 and 2.2.
*   These images will be tagged (e.g., `latest`, version-specific tags like `v1.0.0`) and pushed to a chosen container registry (e.g., `yourusername/sagrafacile-api:latest`, `yourusername/sagrafacile-frontend:latest`).

---

### Phase 3: Orchestration with Docker Compose & Caddy

**Task 3.1: Create `docker-compose.yml` (Repository Root)**
*   Define four services: `db`, `backend`, `frontend`, `caddy`.
    *   **`db` service:**
        *   `image: postgres:15`
        *   `restart: unless-stopped`
        *   `volumes:` map named volume (e.g., `sagrafacile_db_data`) to `/var/lib/postgresql/data`.
        *   `environment:` from `.env` file (`${POSTGRES_USER}`, etc.).
    *   **`api` service (formerly `backend`):**
        *   `image: yourdockerhub_username/sagrafacile-api:latest` # Points to the pre-built image
        *   `restart: unless-stopped`
        *   `depends_on: [db]`
        *   `environment:` from `.env` file.
    *   **`frontend` service:**
        *   `image: yourdockerhub_username/sagrafacile-frontend:latest` # Points to the pre-built image
        *   `restart: unless-stopped`
        *   `environment:` `NEXT_PUBLIC_API_BASE_URL` from `.env` file (e.g., `/api`).
    *   **`caddy` service:**
        *   `image: caddy:2-builder` # Use the builder image to include DNS plugins
        *   `container_name: sagrafacile-caddy`
        *   `restart: unless-stopped`
        *   `ports:` map `"80:80"` and `"443:443"`.
        *   `volumes:`
            *   Map local `Caddyfile` to `/etc/caddy/Caddyfile`.
            *   Map named volume `sagrafacile_caddy_data` to `/data` (for persisting certificates).
        *   `environment:` from `.env` file (`${CLOUDFLARE_API_TOKEN}`, `${MY_DOMAIN}`).
    *   Ports for `api` and `frontend` should not be exposed directly.

**Task 3.2: Create `Caddyfile` (Repository Root)**
*   Configure routing and automatic HTTPS using Let's Encrypt with the Cloudflare DNS challenge.
    ```caddy
    {$MY_DOMAIN} {
        tls {
            dns cloudflare {$CLOUDFLARE_API_TOKEN}
        }

        handle_path /api/* {
            reverse_proxy api:8080 # 'api' is the service name in docker-compose
        }

        handle {
            reverse_proxy frontend:3000 # 'frontend' is the service name
        }
        log {
            output stdout
            format console
        }
    }

    # Optional: HTTP to HTTPS redirect if not handled by default by Caddy for the domain
    # http://{$MY_DOMAIN} {
    #    redir https://{$MY_DOMAIN}{uri}
    # }
    ```

**Task 3.3: Create `.env.example` (Repository Root)**
*   Template with all required environment variables. Ensure it includes:
    *   `MY_DOMAIN=your.domain.com`
    *   `CLOUDFLARE_API_TOKEN=your_cloudflare_api_token_here`
    *   And all other existing necessary variables (`POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`, `JWT_SECRET`, etc.).
    *   Remove any variables specific to Caddy's old `local_certs` setup if they existed.

---

### Phase 4: Deployment Package & User Experience

**Task 4.1: Create User-Facing Helper Scripts**
*   **`start.bat` / `start.sh` (Interactive Setup):**
    1.  **Configuration Check:** On launch, look for `sagrafacile_config.json`.
        *   If found, ask to use existing config, re-configure, or exit.
        *   If not found, proceed to interactive configuration.
    2.  **Interactive Configuration:** Prompt for `MY_DOMAIN`, `CLOUDFLARE_API_TOKEN`, database credentials (`POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`), `JWT_SECRET`.
    3.  Ask about demo data: "Seed demo data? (yes/no)".
        *   If yes, set `SAGRAFACILE_SEED_DEMO_DATA=true`.
        *   If no, prompt for `INITIAL_ADMIN_EMAIL`, `INITIAL_ADMIN_PASSWORD`, `INITIAL_ORGANIZATION_NAME`. Set `SAGRAFACILE_SEED_DEMO_DATA=false`.
    4.  **Save Configuration:** Store all collected values in `sagrafacile_config.json`.
    5.  **Generate `.env`:** Create/overwrite `.env` file using values from `sagrafacile_config.json`.
    6.  **Start Services:** Run `docker-compose up -d`. Caddy will attempt to obtain SSL certificates using Cloudflare.
    7.  Echo instructions for configuring local DNS (router) and app access URL (e.g., `https://your.domain.com`).
*   **`stop.bat` / `stop.sh`:**
    1.  Run `docker-compose down`.
*   **`update.bat` / `update.sh`:**
    1.  Run `docker-compose pull` to get the latest versions of the `api` and `frontend` images.
    2.  Run `docker-compose up -d` to restart services with the new images.

**Task 4.2: Write `README.md` / Installation Guide**
*   Update the guide to reflect the new interactive setup process:
    *   **Prerequisites:**
        *   Docker Desktop (Windows/Mac), Docker Engine (Linux).
        *   A registered domain name (e.g., `my-restaurant-pos.com`).
        *   A Cloudflare account (free tier is sufficient) with your domain added and nameservers updated.
        *   A Cloudflare API Token with "Edit zone DNS" permissions for your domain.
        *   Internet connection (for image download and Let's Encrypt).
    *   **Installation:**
        *   Download and unzip the SagraFacile package.
        *   Run `start.bat` (Windows) or `start.sh` (macOS/Linux - remember `chmod +x *.sh`).
        *   Follow the on-screen prompts to configure your domain, Cloudflare token, database, JWT secret, and initial data preferences. This will create `sagrafacile_config.json` and `.env`.
    *   **MANDATORY - Local DNS Configuration:**
        *   Explain *why*: Devices on your local Wi-Fi need to resolve `your.domain.com` to the *private IP address* of the server running Docker.
        *   Find the server's private IP (e.g., `192.168.1.50`).
        *   Log into your Wi-Fi router.
        *   Find settings like "DNS," "Local DNS," "Static Hostnames," or "DHCP Reservation."
        *   Add an entry mapping `your.domain.com` (the one in `MY_DOMAIN`) to the server's private IP.
        *   This ensures that when a local device tries to access `https://your.domain.com`, it's directed to your local Caddy instance, which serves the valid Let's Encrypt certificate. No certificate installation is needed on client devices because the certificate is publicly trusted.
    *   **Accessing the Application:**
        *   From any device on the local network: `https://your.domain.com`.
    *   **Basic Usage:** `docker-compose down`, `docker-compose up -d`, updating.

**Task 4.3: Package for Distribution**
*   Create a `.zip` file containing:
    *   `docker-compose.yml` (configured to use pre-built images, service `backend` renamed to `api`)
    *   `Caddyfile`
    *   `.env.example` (as a reference)
    *   `start.bat` (new interactive version)
    *   `start.sh` (new interactive version)
    *   `stop.bat`, `stop.sh`
    *   `update.bat`, `update.sh`
    *   `README.md` (the updated installation guide)
    *   `docs/` directory (containing all architecture and supplementary documents)
    *   `sagrafacile_config.json.example` (Optional: an example of the config file structure)
    *   Installer for Windows Printer Service (from Phase 5, or a note if built separately).
    *   **Exclusions:** The ZIP will no longer need to include the `SagraFacile.NET/` or `sagrafacile-webapp/` source code directories for the user. It should still exclude version control directories (e.g., `.git`), IDE-specific folders, etc., from the root package if any are present during packaging.

**Task 4.4: Create GitHub Actions Workflow for ZIP Packaging & Release (e.g., `.github/workflows/release-zip.yml`)**
*   **Trigger:** On push to tags matching `v*.*.*`.
*   **Job:**
    1.  Checkout code.
    2.  (Optional) Build `SagraFacile.WindowsPrinterService.Setup.exe` if it's to be included directly.
    3.  Create the distribution directory and copy files as per Task 4.3.
    4.  Create the ZIP archive (e.g., `SagraFacile-${VERSION_TAG}-dist.zip`).
    5.  Use an action like `softprops/action-gh-release` to create a GitHub Release and upload the ZIP as an asset.
*   **Script Responsibilities (within the workflow or a helper script called by it):**
    *   Define the list of files and directories to include (as per Task 4.3).
    *   Implement logic to exclude specified files and directories (e.g., `.git`, `node_modules`, `bin/`, `obj/`, `.vscode`, `*.log`, etc.). This can be done via direct exclusion flags in the zipping command or by using an ignore file (e.g., a `.distignore` file, if the chosen zipping tool supports it).
    *   Name the output ZIP file consistently (e.g., `SagraFacile-vX.Y.Z-dist.zip`).
*   **Example (Conceptual for a `.sh` script using `zip`):**
    ```bash
    # #!/bin/bash
    # VERSION="1.0.0" # Or get from a file/git tag
    # FILENAME="SagraFacile-v${VERSION}-dist.zip"
    # EXCLUDE_PATTERNS=(
    #   '.git/*' '.vscode/*' '*/node_modules/*'
    #   '*/bin/*' '*/obj/*' '*.DS_Store'
    #   'SagraFacile.NET/SagraFacile.NET.API.Tests.Integration/*' # Exclude test projects
    #   # Add other patterns as needed
    # )
    # 
    # # Convert array to zip exclude options
    # ZIP_EXCLUDE_OPTS=()
    # for pattern in "${EXCLUDE_PATTERNS[@]}"; do
    #   ZIP_EXCLUDE_OPTS+=("-x" "$pattern")
    # done
    # 
    # zip -r "$FILENAME" \
    #   SagraFacile.NET \
    #   sagrafacile-webapp \
    #   docs \
    #   docker-compose.yml \
    #   Caddyfile \
    #   .env.example \
    #   setup.bat \
    #   setup.sh \
    #   README.md \
    #   LICENSE.txt \
    #   "${ZIP_EXCLUDE_OPTS[@]}"
    # 
    # echo "Created $FILENAME"
    ```
*   A similar script could be created for Windows using PowerShell (`Compress-Archive`) or a batch file with a command-line zip utility.

---

### Phase 5: Windows Printer Service (`SagraFacile.WindowsPrinterService`)

Runs parallel to main server setup.

**Task 5.1: Finalize Application**
*   Ensure configurable to point to SagraFacile backend URL (e.g., `https://your.domain.com/api`).
*   The Windows Printer Service will automatically trust the Let's Encrypt certificate as it's publicly valid. No special CA installation is needed for it, provided the Windows machine it runs on has internet access for standard certificate chain validation. If the machine is offline, this could be an issue, but the primary setup assumes internet connectivity for Let's Encrypt.

**Task 5.2: Create Windows Installer (e.g., Inno Setup)**
*   Wizard for backend URL.
*   Install files, create shortcuts, configure run on startup.

**Task 5.3: Update Main Documentation**
*   Add section to main `README.md` about installing `SagraFacile.WindowsPrinterService.Setup.exe` on PCs connected to printers.

## 5. Key Configuration Files Overview

*   **`SagraFacile.NET/SagraFacile.NET.API/Dockerfile`:** (Developer artifact) Defines how the .NET API image (service name `api`) is built by the developer/CI.
*   **`sagrafacile-webapp/Dockerfile`:** (Developer artifact) Defines how the Next.js frontend application image is built by the developer/CI.
*   **`docker-compose.yml`:** (User-facing) Orchestrates the entire application stack by pulling pre-built images for `api`, `frontend`, `caddy`, and `db` (PostgreSQL). Defines services, networks, volumes, and environment variable sourcing from `.env`.
*   **`Caddyfile`:** Configuration for the Caddy reverse proxy (service name `caddy`, container name `sagrafacile-caddy`), handling HTTPS via Let's Encrypt (Cloudflare DNS challenge), and request routing to `api` (container `sagrafacile-api`) and `frontend` (container `sagrafacile-frontend`).
*   **`.env.example`:** A template file showing all necessary environment variables. The actual `.env` file will be generated by the interactive `start` scripts.
*   **`sagrafacile_config.json`:** (User-facing, generated by scripts) Stores user's configuration choices for easy re-deployment.
The database service is named `db` (container `sagrafacile-db`).

## 6. User Setup Workflow Summary (High-Level)

1.  **Prerequisites:**
    *   Install Docker Desktop (Windows/Mac) or Docker Engine (Linux).
    *   A registered domain name.
    *   A Cloudflare account with your domain added and nameservers pointed to Cloudflare.
    *   A Cloudflare API Token with "Edit zone DNS" permission for the domain.
2.  **Download & Unzip:** Obtain the SagraFacile deployment package (e.g., `SagraFacile-vX.Y.Z-dist.zip`) and extract it.
3.  **Run Setup Script:**
    *   Navigate to the extracted folder.
    *   Execute `start.bat` (Windows) or `start.sh` (macOS/Linux - remember to `chmod +x *.sh` first).
4.  **Interactive Configuration:**
    *   Follow the on-screen prompts to provide your domain name, Cloudflare API token, desired database credentials, JWT secret, and choose whether to seed demo data or set up an initial organization/admin user.
    *   The script will save these settings to `sagrafacile_config.json` and generate the necessary `.env` file.
5.  **Start Application:** The script will then start all services using `docker-compose up -d`. Caddy will attempt to obtain a Let's Encrypt certificate using your Cloudflare token.
6.  **Configure Local DNS Resolution:**
    *   Find your server's local IP address (e.g., `192.168.1.50`).
    *   Log into your router and add a DNS entry mapping `MY_DOMAIN` (e.g., `pos.my-restaurant.com`) to the server's local IP address.
7.  **Access Application:** Open `https://your.domain.com` (as specified in `MY_DOMAIN`) in a browser from any device on your local network.
8.  **Install Printer Service (if needed):** For PCs connected to printers, run the `SagraFacile.WindowsPrinterService.Setup.exe`, configuring it to point to `https://your.domain.com/api`.

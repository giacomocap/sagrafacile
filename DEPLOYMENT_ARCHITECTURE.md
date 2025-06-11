# SagraFacile Deployment Architecture

## 1. Introduction

This document outlines the deployment architecture for SagraFacile. The primary goal is to provide a clear, actionable plan for a "Guided Manual Setup" approach, enabling a medium-technical user to deploy the SagraFacile application stack on a Windows machine using Docker.

Considerations for deploying on macOS and Linux systems with Docker are also included, primarily focusing on alternative helper scripts and certificate installation methods.

The deployment strategy focuses on distributing pre-built Docker images for the SagraFacile backend (.NET API) and frontend (Next.js webapp). Users will download a package containing a `docker-compose.yml` file, helper scripts, and configuration templates. The `docker-compose.yml` will pull these pre-built images from a container registry (e.g., Docker Hub, GitHub Container Registry) along with official images for PostgreSQL and Caddy.
This approach simplifies the user's setup significantly by removing the need for local builds.

## 2. Core Technologies

*   **Docker:** For containerizing individual application components (backend, frontend, database).
*   **Docker Compose:** For defining and orchestrating the multi-container application stack.
*   **Caddy Web Server:** As a reverse proxy to manage incoming traffic, provide automatic HTTPS (using self-signed certificates for local deployment), and route requests to the appropriate services.

## 3. Overall Architecture (Conceptual Flow)

```
End User Browser (HTTPS)
       |
       v
+-----------------+
| Caddy           | (Reverse Proxy, SSL Termination)
| (Container)     |
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
*   **Initial Admin User (Optional):** Implement logic to create an initial admin user from environment variables (e.g., `INITIAL_ADMIN_EMAIL`, `INITIAL_ADMIN_PASSWORD`) if no admin users exist.

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
    *   **`backend` service:**
        *   `image: yourdockerhub_username/sagrafacile-api:latest` # Points to the pre-built image
        *   `restart: unless-stopped`
        *   `depends_on: [db]`
        *   `environment:` from `.env` file.
    *   **`frontend` service:**
        *   `image: yourdockerhub_username/sagrafacile-frontend:latest` # Points to the pre-built image
        *   `restart: unless-stopped`
        *   `environment:` `NEXT_PUBLIC_API_BASE_URL` from `.env` file (e.g., `/api`).
    *   **`caddy` service:**
        *   `image: caddy:2-alpine` # Using alpine for smaller size
        *   `container_name: sagrafacile_caddy` (for consistent certificate extraction)
        *   `restart: unless-stopped`
        *   `ports:` map `"80:80"` and `"443:443"`.
        *   `volumes:`
            *   Map local `Caddyfile` to `/etc/caddy/Caddyfile`.
            *   Map named volume `sagrafacile_caddy_data` to `/data`.
            *   Map named volume `sagrafacile_caddy_config` to `/config`.

**Task 3.2: Create `Caddyfile` (Repository Root)**
*   Configure routing and automatic HTTPS with Caddy's internal CA.
    ```caddy
    {
        local_certs
    }

    :443 {
        handle_path /api/* {
            reverse_proxy backend:8080 # Target backend's HTTP port
        }
        
        handle {
            reverse_proxy frontend:3000
        }

        tls internal
        log {
            output stdout
            format console
        }
    }

    :80 {
        redir https://{host}{uri}
    }
    ```

**Task 3.3: Create `.env.example` (Repository Root)**
*   Template with all required environment variables, comments, and placeholders (e.g., `JWT_SECRET=CHANGE_THIS_TO_A_VERY_LONG_RANDOM_STRING`).

---

### Phase 4: Deployment Package & User Experience

**Task 4.1: Create User-Facing Helper Scripts**
*   **`start.bat` / `start.sh`:**
    1.  Check if `.env` exists; if not, guide the user to copy `.env.example` to `.env` and configure it.
    2.  Run `docker-compose up -d`. This will pull images if they are not present locally.
    3.  Echo instructions for CA certificate installation and app access URL.
*   **`stop.bat` / `stop.sh`:**
    1.  Run `docker-compose down`.
*   **`update.bat` / `update.sh`:**
    1.  Run `docker-compose pull` to get the latest versions of the `backend` and `frontend` images.
    2.  Run `docker-compose up -d` to restart services with the new images.

**Task 4.2: Write `README.md` / Installation Guide**
*   Update the guide to reflect the new simplified process:
    *   **Prerequisites:** Docker Desktop (Windows/Mac), Docker Engine (Linux), Internet connection (for image download).
    *   **Installation:**
        *   Download and unzip the SagraFacile package.
        *   Copy `.env.example` to `.env` and configure it.
        *   Run `start.bat` (Windows) or `start.sh` (macOS/Linux), including `chmod +x *.sh` for Linux/macOS.
    *   **MANDATORY - Trusting the Security Certificate (Platform-Specific):**
        *   Explain *why* (for `https://localhost` or `https://<local-ip>`).
        *   Windows: `docker cp sagrafacile_caddy:/data/caddy/pki/authorities/local/root.crt .` then `certutil -addstore -f "ROOT" "root.crt"` (Admin Prompt).
        *   macOS: `docker cp sagrafacile_caddy:/data/caddy/pki/authorities/local/root.crt .` then `sudo security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain root.crt`.
        *   Linux: `docker cp sagrafacile_caddy:/data/caddy/pki/authorities/local/root.crt .` then provide general instructions (e.g., copy to `/usr/local/share/ca-certificates/` and run `sudo update-ca-certificates`, noting variance by distribution).
    *   **Connecting Other Devices:** Explain browser warning and need to install `root.crt` on them.
    *   **Basic Usage:** `docker-compose down`, `docker-compose up -d`, updating.

**Task 4.3: Package for Distribution**
*   Create a `.zip` file containing:
    *   `docker-compose.yml` (configured to use pre-built images)
    *   `Caddyfile`
    *   `.env.example`
    *   `start.bat`, `start.sh`
    *   `stop.bat`, `stop.sh`
    *   `update.bat`, `update.sh`
    *   `README.md` (the updated installation guide)
    *   `docs/` directory (containing all architecture and supplementary documents)
    *   Installer for Windows Printer Service (from Phase 5).
    *   **Exclusions:** The ZIP will no longer need to include the `SagraFacile.NET/` or `sagrafacile-webapp/` source code directories for the user. It should still exclude version control directories (e.g., `.git`), IDE-specific folders, etc., from the root package if any are present during packaging.

**Task 4.4: Create ZIP Packaging Script(s) (Optional but Recommended)**
*   To ensure consistency and simplify the creation of the distribution `.zip` file, consider creating a script (or scripts for different OS environments) to automate the packaging process.
*   **Script Responsibilities:**
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
*   Ensure configurable to point to SagraFacile backend URL (e.g., `https://localhost/api` or `https://<host-ip>/api`).
*   Ensure it trusts the custom root CA once installed on the host Windows machine.

**Task 5.2: Create Windows Installer (e.g., Inno Setup)**
*   Wizard for backend URL.
*   Install files, create shortcuts, configure run on startup.

**Task 5.3: Update Main Documentation**
*   Add section to main `README.md` about installing `SagraFacile.WindowsPrinterService.Setup.exe` on PCs connected to printers.

## 5. Key Configuration Files Overview

*   **`SagraFacile.NET/SagraFacile.NET.API/Dockerfile`:** (Developer artifact) Defines how the .NET backend API image is built by the developer/CI.
*   **`sagrafacile-webapp/Dockerfile`:** (Developer artifact) Defines how the Next.js frontend application image is built by the developer/CI.
*   **`docker-compose.yml`:** (User-facing) Orchestrates the entire application stack by pulling pre-built images for backend, frontend, Caddy, and PostgreSQL. Defines services, networks, volumes, and environment variable sourcing.
*   **`Caddyfile`:** Configuration for the Caddy reverse proxy, handling HTTPS, and request routing.
*   **`.env.example`:** A template file showing all necessary environment variables for the application stack. Users will copy this to `.env` and customize it.

## 6. User Setup Workflow Summary (High-Level)

1.  **Prerequisites:** Install Docker Desktop (Windows/Mac) or Docker Engine (Linux).
2.  **Download & Unzip:** Obtain the SagraFacile deployment package (e.g., `SagraFacile-vX.Y.Z.zip`) and extract it.
3.  **Configure Environment:** Navigate to the extracted folder, copy `.env.example` to `.env`, and edit `.env` with specific settings (database passwords, JWT secrets, etc.).
4.  **Start Application:** Execute `start.bat` (Windows) or `start.sh` (macOS/Linux - remember to `chmod +x *.sh` first). This will pull images and start services.
5.  **Install CA Certificate:** Follow instructions provided by the start script and in `README.md` to install the Caddy-generated root CA certificate on the host machine (and any client devices) to trust the local HTTPS connection.
7.  **Access Application:** Open `https://localhost` (or `https://<host-ip>`) in a browser.
8.  **Install Printer Service (if needed):** For PCs connected to printers, run the `SagraFacile.WindowsPrinterService.Setup.exe`.

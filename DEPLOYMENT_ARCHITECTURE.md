# SagraFacile Deployment Architecture

## 1. Introduction

This document outlines the deployment architecture for SagraFacile. The primary goal is to provide a clear, actionable plan for a "Guided Manual Setup" approach, enabling a medium-technical user to deploy the SagraFacile application stack on a Windows machine using Docker.

Considerations for deploying on macOS and Linux systems with Docker are also included, primarily focusing on alternative helper scripts and certificate installation methods.

The deployment will package the SagraFacile backend (.NET API), frontend (Next.js webapp), a PostgreSQL database, and a Caddy reverse proxy into a distributable format.

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

### Phase 2: Dockerization

Create `Dockerfile`s for custom applications.

**Task 2.1: Create/Update Backend Dockerfile (`SagraFacile.NET/SagraFacile.NET.API/Dockerfile`)**
*   Use a multi-stage build.
    1.  **Build Stage:** Use `.NET SDK` image (`mcr.microsoft.com/dotnet/sdk:9.0`) to restore, build, and publish `SagraFacile.NET.API.csproj`.
    2.  **Final Stage:** Use `ASP.NET Runtime` image (`mcr.microsoft.com/dotnet/aspnet:9.0`). Copy published output.
    3.  Set `ENTRYPOINT` to `["dotnet", "SagraFacile.NET.API.dll"]`.
    4.  Expose internal port `8080` (HTTP).
*   *Note: The existing Dockerfile will be modified to ensure correct project names (`SagraFacile.NET.API.csproj`) and paths are used.*

**Task 2.2: Create Frontend Dockerfile (`sagrafacile-webapp/Dockerfile`)**
*   Use a multi-stage build.
    1.  **Build Stage:** Use Node.js image (e.g., `node:20-alpine`) for `npm install` and `npm run build`.
    2.  **Final Stage:** Use a smaller Node.js image. Copy `.next` directory, `public` directory, `package.json`, `package-lock.json` (if applicable). Run `npm install --production`.
    3.  Set `CMD` to `npm start`. Expose internal port `3000`.

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
        *   `build: ./SagraFacile.NET` (or `./SagraFacile.NET/SagraFacile.NET.API` if Dockerfile is nested and context needs to be specific)
        *   `restart: unless-stopped`
        *   `depends_on: [db]`
        *   `environment:` from `.env` file.
    *   **`frontend` service:**
        *   `build: ./sagrafacile-webapp`
        *   `restart: unless-stopped`
        *   `environment:` `NEXT_PUBLIC_API_BASE_URL` from `.env` file.
    *   **`caddy` service:**
        *   `image: caddy:2`
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

**Task 4.1: Create Helper Scripts**
*   **`setup.bat` (Windows):**
    1.  Check if `.env` exists; if not, copy `.env.example` to `.env`.
    2.  Prompt user to edit `.env` and press Enter.
    3.  Run `docker-compose up -d --build`.
    4.  Echo instructions for CA certificate installation and app access URL.
*   **`setup.sh` (Linux/macOS):**
    1.  Similar logic to `setup.bat` using shell commands (`cp`, `echo`, `read`).
    2.  Provide platform-specific CA certificate installation instructions or commands.

**Task 4.2: Write `README.md` / Installation Guide**
*   Structure for clarity:
    *   **Prerequisites:** Docker Desktop (Windows/Mac), Docker Engine (Linux). Links to official installation guides.
    *   **Installation (Platform-Specific Sections):**
        *   Windows: Unzip, run `setup.bat`, follow prompts.
        *   macOS/Linux: Unzip, run `setup.sh`, follow prompts.
    *   **MANDATORY - Trusting the Security Certificate (Platform-Specific):**
        *   Explain *why* (for `https://localhost` or `https://<local-ip>`).
        *   Windows: `docker cp sagrafacile_caddy:/data/caddy/pki/authorities/local/root.crt .` then `certutil -addstore -f "ROOT" "root.crt"` (Admin Prompt).
        *   macOS: `docker cp sagrafacile_caddy:/data/caddy/pki/authorities/local/root.crt .` then `sudo security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain root.crt`.
        *   Linux: `docker cp sagrafacile_caddy:/data/caddy/pki/authorities/local/root.crt .` then provide general instructions (e.g., copy to `/usr/local/share/ca-certificates/` and run `sudo update-ca-certificates`, noting variance by distribution).
    *   **Connecting Other Devices:** Explain browser warning and need to install `root.crt` on them.
    *   **Basic Usage:** `docker-compose down`, `docker-compose up -d`, updating.

**Task 4.3: Package for Distribution**
*   Create a `.zip` file containing:
    *   `SagraFacile.NET/` (source for backend build context)
    *   `sagrafacile-webapp/` (source for frontend build context)
    *   `docker-compose.yml`
    *   `Caddyfile`
    *   `.env.example`
    *   `setup.bat`
    *   `setup.sh`
    *   `README.md` (the detailed installation guide)
    *   Installer for Windows Printer Service (from Phase 5).

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

*   **`SagraFacile.NET/SagraFacile.NET.API/Dockerfile`:** Defines how the .NET backend API is containerized.
*   **`sagrafacile-webapp/Dockerfile`:** Defines how the Next.js frontend application is containerized.
*   **`docker-compose.yml`:** Orchestrates the entire application stack (database, backend, frontend, caddy proxy). Defines services, networks, volumes, and environment variable sourcing.
*   **`Caddyfile`:** Configuration for the Caddy reverse proxy, handling HTTPS, and request routing.
*   **`.env.example`:** A template file showing all necessary environment variables for the application stack. Users will copy this to `.env` and customize it.

## 6. User Setup Workflow Summary (High-Level)

1.  **Prerequisites:** Install Docker Desktop (Windows/Mac) or Docker Engine (Linux).
2.  **Download & Unzip:** Obtain the SagraFacile deployment package and extract it.
3.  **Run Setup Script:** Execute `setup.bat` (Windows) or `setup.sh` (macOS/Linux).
4.  **Configure Environment:** Edit the created `.env` file with specific settings (database passwords, JWT secrets, etc.) when prompted by the script.
5.  **Start Application:** The setup script will run `docker-compose up -d --build` to build and start all services.
6.  **Install CA Certificate:** Follow instructions provided by the setup script and in `README.md` to install the Caddy-generated root CA certificate on the host machine (and any client devices) to trust the local HTTPS connection.
7.  **Access Application:** Open `https://localhost` (or `https://<host-ip>`) in a browser.
8.  **Install Printer Service (if needed):** For PCs connected to printers, run the `SagraFacile.WindowsPrinterService.Setup.exe`.

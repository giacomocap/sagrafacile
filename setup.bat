@echo off
REM SagraFacile Setup Script for Windows

echo SagraFacile Setup
echo ==================
echo.

REM Check for .env file
IF NOT EXIST .env (
    echo Creating .env file from .env.example...
    copy .env.example .env
    IF ERRORLEVEL 1 (
        echo ERROR: Could not copy .env.example to .env. Please do this manually.
        pause
        exit /b 1
    )
    echo .env file created.
) ELSE (
    echo .env file already exists.
)
echo.
echo IMPORTANT: Please open the .env file in a text editor and
echo configure your settings (database passwords, JWT secrets, etc.).
echo Ensure all placeholders like "CHANGE_THIS_..." are replaced.
echo.
pause

echo.
echo Starting SagraFacile services using Docker Compose...
echo This may take a while on the first run as images are built.
echo.
docker compose up -d --build

IF ERRORLEVEL 1 (
    echo ERROR: Docker Compose failed to start. Please check the output above for errors.
    pause
    exit /b 1
)

echo.
echo SagraFacile services started successfully!
echo.
echo ====================================================================
echo MANDATORY NEXT STEP: TRUST THE SELF-SIGNED HTTPS CERTIFICATE
echo ====================================================================
echo To access SagraFacile via HTTPS (e.g., https://localhost or https://your-local-ip),
echo you MUST install the Caddy-generated root CA certificate on this computer
echo and on any other devices you plan to use to access the application.
echo.
echo Instructions for Windows (run these commands in an Administrator Command Prompt or PowerShell):
echo.
echo 1. Copy the certificate from the Caddy container:
echo    docker cp sagrafacile_caddy:/data/caddy/pki/authorities/local/root.crt .
echo.
echo 2. Install the certificate:
echo    certutil -addstore -f "ROOT" "root.crt"
echo.
echo 3. (Optional) Delete the copied certificate file after installation:
echo    del root.crt
echo.
echo After installing the certificate, you might need to restart your browser.
echo ====================================================================
echo.
echo For detailed advice on configuring your local network (router settings, DHCP, static IPs),
echo please refer to the 'docs/NetworkingArchitecture.md' file in this package.
echo It is highly recommended to assign a static IP address to the computer running SagraFacile.
echo.
echo Access SagraFacile at: https://localhost
echo Or, if accessing from another device on your network, use: https://%COMPUTERNAME% or https://<your-host-machine-ip-address>
echo (You can find your IP address by typing 'ipconfig' in a command prompt. Ensure this IP is static.)
echo.
echo To stop SagraFacile, run: docker compose down
echo To stop and remove data volumes (deletes database!), run: docker compose down -v
echo.
pause

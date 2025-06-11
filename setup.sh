#!/bin/bash
# SagraFacile Setup Script for Linux/macOS

echo "SagraFacile Setup"
echo "=================="
echo

# Check for .env file
if [ ! -f .env ]; then
    echo "Creating .env file from .env.example..."
    cp .env.example .env
    if [ $? -ne 0 ]; then
        echo "ERROR: Could not copy .env.example to .env. Please do this manually."
        exit 1
    fi
    echo ".env file created."
else
    echo ".env file already exists."
fi
echo
echo "IMPORTANT: Please open the .env file in a text editor (e.g., nano .env, vim .env, or your favorite GUI editor)"
echo "and configure your settings (database passwords, JWT secrets, etc.)."
echo "Ensure all placeholders like \"CHANGE_THIS_...\" are replaced."
echo
read -p "Press Enter to continue after editing .env..."

echo
echo "Starting SagraFacile services using Docker Compose..."
echo "This may take a while on the first run as images are built."
echo
docker compose up -d --build

if [ $? -ne 0 ]; then
    echo "ERROR: Docker Compose failed to start. Please check the output above for errors."
    exit 1
fi

echo
echo "SagraFacile services started successfully!"
echo
echo "===================================================================="
echo "MANDATORY NEXT STEP: TRUST THE SELF-SIGNED HTTPS CERTIFICATE"
echo "===================================================================="
echo "To access SagraFacile via HTTPS (e.g., https://localhost or https://your-local-ip),"
echo "you MUST install the Caddy-generated root CA certificate on this computer"
echo "and on any other devices you plan to use to access the application."
echo
echo "Instructions (run these commands in your terminal):"
echo
echo "1. Copy the certificate from the Caddy container:"
echo "   docker cp sagrafacile_caddy:/data/caddy/pki/authorities/local/root.crt ."
echo
echo "2. Install the certificate:"
echo "   - On macOS:"
echo "     sudo security add-trusted-cert -d -r trustRoot -k /Library/Keychains/System.keychain root.crt"
echo "   - On Linux (Debian/Ubuntu based - may vary for other distributions):"
echo "     sudo mkdir -p /usr/local/share/ca-certificates/extra"
echo "     sudo cp root.crt /usr/local/share/ca-certificates/extra/sagrafacile-local-root.crt"
echo "     sudo update-ca-certificates"
echo "     (For other Linux distributions, please consult your system's documentation for adding CA certificates.)"
echo
echo "3. (Optional) Delete the copied certificate file after installation:"
echo "   rm root.crt"
echo
echo "After installing the certificate, you might need to restart your browser."
echo "===================================================================="
echo
echo "For detailed advice on configuring your local network (router settings, DHCP, static IPs),"
echo "please refer to the 'docs/NetworkingArchitecture.md' file in this package."
echo "It is highly recommended to assign a static IP address to the computer running SagraFacile."
echo
echo "Access SagraFacile at: https://localhost"
echo "Or, if accessing from another device on your network, use: https://$(hostname) or https://<your-host-machine-ip-address>"
echo "(You can find your IP address using 'ifconfig' or 'ip addr' in the terminal. Ensure this IP is static.)"
echo
echo "To stop SagraFacile, run: docker compose down"
echo "To stop and remove data volumes (deletes database!), run: docker compose down -v"
echo

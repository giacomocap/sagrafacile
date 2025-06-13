#!/bin/bash

echo "Starting SagraFacile..."
echo "Ensuring Docker services are running..."
echo

# Check if .env file exists, if not, guide user
if [ ! -f .env ]; then
    echo "WARNING: .env file not found!"
    echo "Please copy .env.example to .env and configure it with your settings."
    echo "Press Enter to exit and configure .env, then re-run start.sh."
    read -r
    exit 1
fi

docker compose up -d
echo
echo "SagraFacile services are starting up."
echo "Caddy will attempt to obtain a Let's Encrypt SSL certificate for your domain."
echo "This may take a few moments, especially on the first run."
echo
echo "Once all services are running:"
echo "- You should be able to access the application at: https://\${MY_DOMAIN}"
echo "  (Ensure MY_DOMAIN is correctly set in your .env file, e.g., pos.my-restaurant-pos.com)"
echo
echo "IMPORTANT FOR LOCAL NETWORK ACCESS:"
echo "To access SagraFacile from other devices on your local network using https://\${MY_DOMAIN},"
echo "you MUST configure your router's Local DNS settings to point \${MY_DOMAIN}"
echo "to the local IP address of this server (e.g., 192.168.1.50)."
echo "Detailed instructions are in the README.md file."
echo
echo "If Caddy fails to obtain a certificate, check its logs: docker compose logs -f caddy"
echo "Ensure your domain is correctly pointing to your public IP and Cloudflare API token is valid."
echo
echo "To view all logs, run: docker compose logs -f"
echo "To stop services, run: ./stop.sh"
echo
echo "Press Enter to exit this script (services will continue running in the background)."
read -r

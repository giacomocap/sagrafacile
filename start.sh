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

docker-compose up -d
echo
echo "SagraFacile services are starting up."
echo
echo "Once all services are running (this may take a moment on first start):"
echo "- Access the application at: https://localhost"
echo "- If you are accessing from another device on your network, use: https://[SERVER-IP-ADDRESS]"
echo "  (Replace [SERVER-IP-ADDRESS] with the actual IP address of this computer)"
echo
echo "IMPORTANT: If this is your first time running, or if you see certificate errors,"
echo "please ensure you have installed the Caddy root CA certificate on this machine"
echo "and on any client devices. Instructions are in the README.md file."
echo
echo "To view logs, run: docker-compose logs -f"
echo "To stop services, run: ./stop.sh"
echo
echo "Press Enter to exit this script (services will continue running in the background)."
read -r

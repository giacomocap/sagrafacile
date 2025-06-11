#!/bin/bash

echo "Starting SagraFacile..."
echo "Ensuring Docker services are running..."
echo

# Check if .env file exists, if not, copy from .env.example
if [ ! -f .env ]; then
    echo "INFO: .env file not found. Copying from .env.example..."
    if cp .env.example .env; then
        echo "INFO: .env file created successfully."
    else
        echo "ERROR: Failed to copy .env.example to .env. Please do this manually."
        exit 1
    fi
fi

# Check for JWT_SECRET in .env and generate if missing
JWT_SECRET_EXISTS=$(grep -E "^JWT_SECRET=.+" .env)
if [ -z "$JWT_SECRET_EXISTS" ]; then
    echo "INFO: JWT_SECRET not found or is empty in .env file."
    if command -v openssl &> /dev/null; then
        echo "INFO: Generating a new JWT_SECRET..."
        NEW_SECRET=$(openssl rand -hex 32)
        if [ -n "$NEW_SECRET" ]; then
            echo "" >> .env # Add a newline for separation if file doesn't end with one
            echo "JWT_SECRET=$NEW_SECRET" >> .env
            echo "INFO: A new JWT_SECRET has been generated and saved to .env."
            echo "IMPORTANT: If you run SagraFacile on multiple servers or instances that need to share authentication,"
            echo "ensure this JWT_SECRET is the same across all of them. You may need to copy it from this .env file."
        else
            echo "ERROR: Failed to generate JWT_SECRET using openssl."
            echo "Please generate a secret manually (e.g., using 'openssl rand -hex 32') and add it to your .env file as JWT_SECRET=your_secret_here"
            exit 1
        fi
    else
        echo "WARNING: openssl command not found. Cannot automatically generate JWT_SECRET."
        echo "Please generate a secret manually (e.g., using 'openssl rand -hex 32' on a system with openssl)"
        echo "and add it to your .env file as JWT_SECRET=your_secret_here"
        echo "Press Enter to continue without automatic generation (application might not work correctly), or Ctrl+C to exit and set it manually."
        read -r
    fi
else
    echo "INFO: JWT_SECRET found in .env file."
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

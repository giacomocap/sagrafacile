#!/bin/bash

echo "Pulling the latest SagraFacile updates..."
docker compose pull
echo
echo "Applying updates and restarting the application..."
docker compose up -d
echo
echo "SagraFacile update complete!"
echo "The application should now be running with the latest version."
echo
echo "Access the application at: https://localhost"
echo "(or https://[SERVER-IP-ADDRESS] from other devices)"
echo
echo "Press Enter to exit this script."
read -r

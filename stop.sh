#!/bin/bash

echo "Stopping SagraFacile services..."
docker-compose down
echo
echo "SagraFacile services have been stopped."
echo
echo "Press Enter to exit this script."
read -r

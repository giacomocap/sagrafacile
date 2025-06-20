#!/bin/bash

# --- Configuration ---
# !!! IMPORTANT: SET THESE VARIABLES !!!
SERVER_USER="root"                 # Your username on the test server
SERVER_IP="192.168.1.23"    # IP address or hostname of your test server
# !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!

# --- Script Variables ---
LOCAL_IMAGE_NAME="sagrafacile-api-localtest"
LOCAL_IMAGE_TAG="latest"
TARBALL_NAME="${LOCAL_IMAGE_NAME}.tar"

# Paths (relative to the sagrafacile project root where this script is located)
DOCKER_CONTEXT_PATH="./SagraFacile.NET"
DOCKERFILE_PATH="./SagraFacile.NET/SagraFacile.NET.API/Dockerfile"

# Remote paths on the server
if [ "$SERVER_USER" == "root" ]; then
    REMOTE_BASE_PATH="/root/sagrafacile"
else
    REMOTE_BASE_PATH="~/sagrafacile" # Assuming your project is in ~/sagrafacile for non-root users
fi
REMOTE_TARBALL_PATH="${REMOTE_BASE_PATH}/${TARBALL_NAME}"
REMOTE_OVERRIDE_FILENAME="docker-compose.localtest.override.yml"
REMOTE_OVERRIDE_FILE_PATH="${REMOTE_BASE_PATH}/${REMOTE_OVERRIDE_FILENAME}"

# --- Functions ---
print_step() {
    echo
    echo "----------------------------------------"
    echo "$1"
    echo "----------------------------------------"
}

# --- Main Script ---
print_step "Starting local test deployment script..."

# Validate configuration
if [ "$SERVER_USER" == "your_username" ] || [ "$SERVER_IP" == "your_server_ip_or_hostname" ]; then
    echo "ERROR: Please update SERVER_USER and SERVER_IP variables in this script."
    exit 1
fi

# 1. Build the Docker image locally
print_step "1. Building Docker image for linux/amd64: ${LOCAL_IMAGE_NAME}:${LOCAL_IMAGE_TAG}"
# Add --platform linux/amd64 to build for your server's architecture
if ! docker build --platform linux/amd64 -t "${LOCAL_IMAGE_NAME}:${LOCAL_IMAGE_TAG}" -f "${DOCKERFILE_PATH}" "${DOCKER_CONTEXT_PATH}"; then
    echo "ERROR: Docker build failed."
    exit 1
fi

# 2. Save the image to a .tar file
print_step "2. Saving image to ${TARBALL_NAME}"
if ! docker save "${LOCAL_IMAGE_NAME}:${LOCAL_IMAGE_TAG}" -o "${TARBALL_NAME}"; then
    echo "ERROR: Docker save failed."
    rm -f "${TARBALL_NAME}" # Clean up partial tarball if save failed
    exit 1
fi

# 3. Transfer the .tar file to the server
print_step "3. Transferring ${TARBALL_NAME} to ${SERVER_USER}@${SERVER_IP}:${REMOTE_TARBALL_PATH}"
if ! scp "${TARBALL_NAME}" "${SERVER_USER}@${SERVER_IP}:${REMOTE_TARBALL_PATH}"; then
    echo "ERROR: SCP transfer failed."
    rm -f "${TARBALL_NAME}"
    exit 1
fi

# 4. SSH to server to load image, create override, and restart service
print_step "4. Executing remote commands on server..."

# Content for the docker-compose.localtest.override.yml
OVERRIDE_CONTENT="services:
  api:
    image: ${LOCAL_IMAGE_NAME}:${LOCAL_IMAGE_TAG}"

# SSH command to execute on the server
# - Loads the image
# - Creates the override file
# - Stops the current api service (if running with the override)
# - Starts the api service with the new image using the override, forcing recreation
# - Tails logs
# Note: Using '&&' ensures commands run sequentially and stop if one fails.
# Using 'set -e' on the remote side makes the remote script part exit on error.
SSH_COMMANDS="set -e; \
echo '--- Loading Docker image on server ---'; \
docker load -i '${REMOTE_TARBALL_PATH}'; \
echo '--- Creating/Updating ${REMOTE_OVERRIDE_FILENAME} on server ---'; \
echo -e \"${OVERRIDE_CONTENT}\" > '${REMOTE_OVERRIDE_FILE_PATH}'; \
echo '--- Restarting api service with local image ---'; \
cd '${REMOTE_BASE_PATH}' && docker compose -f docker-compose.yml -f '${REMOTE_OVERRIDE_FILENAME}' up -d --force-recreate --remove-orphans api; \
echo '--- API service updated. Tailing logs (Ctrl+C to stop tailing) ---'; \
cd '${REMOTE_BASE_PATH}' && docker compose logs -f api"

if ! ssh "${SERVER_USER}@${SERVER_IP}" "${SSH_COMMANDS}"; then
    echo "ERROR: SSH command execution failed or was interrupted."
    # Note: Local tarball is cleaned up regardless of SSH success/failure if SCP was successful.
fi

# 5. Clean up local .tar file
print_step "5. Cleaning up local tarball: ${TARBALL_NAME}"
rm -f "${TARBALL_NAME}"

print_step "Script finished."
echo "To revert to the production image on the server, you can either:"
echo "  a) Remove or empty '${REMOTE_OVERRIDE_FILE_PATH}' on the server and run 'docker compose up -d api'."
echo "  b) Or, if you pushed a new official image, run 'docker compose pull api && docker compose up -d api' (after removing the override)."

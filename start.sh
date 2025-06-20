#!/bin/bash

CONFIG_FILE="sagrafacile_config.json"
ENV_FILE=".env"

# Function to prompt for user input with a default value
# Arg1: prompt_message
# Arg2: variable_name
# Arg3: default_value
# Arg4: is_optional (true/false) - if true, allows empty input
prompt_for_input() {
    local prompt_message="$1"
    local variable_name="$2"
    local default_value="$3"
    local is_optional="${4:-false}" # Default to not optional
    local current_value
    local input # Temporary variable to store user input

    eval current_value="\$$variable_name" # Get current value of the variable to display in prompt if default_value is not passed explicitly

    if [ -n "$default_value" ]; then
        read -r -p "$prompt_message [$default_value]: " input
        eval "$variable_name=\"\${input:-$default_value}\""
    else
        # No explicit default value provided in the call
        # Use current_value as the display default if it exists
        if [ -n "$current_value" ]; then
             read -r -p "$prompt_message [$current_value]: " input
             eval "$variable_name=\"\${input:-$current_value}\""
        else
            read -r -p "$prompt_message: " input
            eval "$variable_name=\"\$input\""
        fi

        if [ "$is_optional" != true ] && [ -z "$(eval echo \$$variable_name)" ]; then
            # Loop only if not optional and is empty
            while [ -z "$(eval echo \$$variable_name)" ]; do
                echo "This field cannot be empty."
                read -r -p "$prompt_message: " input
                eval "$variable_name=\"\$input\""
            done
        fi
    fi
}

# Function to prompt for yes/no
prompt_yes_no() {
    local prompt_message="$1"
    local variable_name="$2"
    local default_value="$3" # "yes" or "no"

    while true; do
        if [ "$default_value" == "yes" ]; then
            read -r -p "$prompt_message (Y/n): " choice
            choice=${choice:-Y}
        elif [ "$default_value" == "no" ]; then
            read -r -p "$prompt_message (y/N): " choice
            choice=${choice:-N}
        else
            read -r -p "$prompt_message (y/n): " choice
        fi

        case "$choice" in
            [Yy]* ) eval "$variable_name=true"; break;;
            [Nn]* ) eval "$variable_name=false"; break;;
            * ) echo "Please answer yes (y) or no (n).";;
        esac
    done
}

# Function to generate a random JWT secret
generate_jwt_secret() {
    # Attempt to use OpenSSL if available, otherwise a simpler method
    if command -v openssl &> /dev/null; then
        openssl rand -base64 32
    else
        # Fallback for systems without openssl easily available in path
        # This is not cryptographically strong for production but better than nothing for a default.
        LC_ALL=C tr -dc 'A-Za-z0-9!"#$%&'\''()*+,-./:;<=>?@[\]^_`{|}~' </dev/urandom | head -c 32 ; echo
    fi
}

# Function to load config from JSON (basic parsing, assumes simple structure)
# This is a simplified loader. For complex JSON, jq would be better.
load_config_from_json() {
    if [ -f "$CONFIG_FILE" ]; then
        MY_DOMAIN=$(grep -oP '"MY_DOMAIN": "\K[^"]*' $CONFIG_FILE || echo "")
        CLOUDFLARE_API_TOKEN=$(grep -oP '"CLOUDFLARE_API_TOKEN": "\K[^"]*' $CONFIG_FILE || echo "")
        POSTGRES_USER=$(grep -oP '"POSTGRES_USER": "\K[^"]*' $CONFIG_FILE || echo "sagrafacile")
        POSTGRES_PASSWORD=$(grep -oP '"POSTGRES_PASSWORD": "\K[^"]*' $CONFIG_FILE || echo "sagrafacilepass")
        POSTGRES_DB=$(grep -oP '"POSTGRES_DB": "\K[^"]*' $CONFIG_FILE || echo "sagrafaciledb")
        JWT_SECRET=$(grep -oP '"JWT_SECRET": "\K[^"]*' $CONFIG_FILE || echo "")
        SAGRAFACILE_SEED_DEMO_DATA=$(grep -oP '"SAGRAFACILE_SEED_DEMO_DATA": \K(true|false)' $CONFIG_FILE || echo "false")
        INITIAL_ORGANIZATION_NAME=$(grep -oP '"INITIAL_ORGANIZATION_NAME": "\K[^"]*' $CONFIG_FILE || echo "")
        INITIAL_ADMIN_EMAIL=$(grep -oP '"INITIAL_ADMIN_EMAIL": "\K[^"]*' $CONFIG_FILE || echo "")
        INITIAL_ADMIN_PASSWORD=$(grep -oP '"INITIAL_ADMIN_PASSWORD": "\K[^"]*' $CONFIG_FILE || echo "")
        
        # Advanced/Optional settings from config
        JWT_ISSUER_CFG=$(grep -oP '"JWT_ISSUER": "\K[^"]*' $CONFIG_FILE || echo "SagraFacile")
        JWT_AUDIENCE_CFG=$(grep -oP '"JWT_AUDIENCE": "\K[^"]*' $CONFIG_FILE || echo "SagraFacileApp")
        ENABLE_PREORDER_POLLING_SERVICE_CFG=$(grep -oP '"ENABLE_PREORDER_POLLING_SERVICE": \K(true|false)' $CONFIG_FILE || echo "true")
        CLOUDFLARE_EMAIL_CFG=$(grep -oP '"CLOUDFLARE_EMAIL": "\K[^"]*' $CONFIG_FILE || echo "")
        SUPERADMIN_EMAIL_CFG=$(grep -oP '"SUPERADMIN_EMAIL": "\K[^"]*' $CONFIG_FILE || echo "superadmin@example.com")
        SUPERADMIN_PASSWORD_CFG=$(grep -oP '"SUPERADMIN_PASSWORD": "\K[^"]*' $CONFIG_FILE || echo "SuperAdminPass123!")
        DEMO_USER_PASSWORD_CFG=$(grep -oP '"DEMO_USER_PASSWORD": "\K[^"]*' $CONFIG_FILE || echo "DemoUserPass123!")

        # Assign to main variables, respecting existing values if already set (e.g. during reconfigure prompt)
        JWT_ISSUER=${JWT_ISSUER:-$JWT_ISSUER_CFG}
        JWT_AUDIENCE=${JWT_AUDIENCE:-$JWT_AUDIENCE_CFG}
        ENABLE_PREORDER_POLLING_SERVICE=${ENABLE_PREORDER_POLLING_SERVICE:-$ENABLE_PREORDER_POLLING_SERVICE_CFG}
        CLOUDFLARE_EMAIL=${CLOUDFLARE_EMAIL:-$CLOUDFLARE_EMAIL_CFG}
        SUPERADMIN_EMAIL=${SUPERADMIN_EMAIL:-$SUPERADMIN_EMAIL_CFG}
        SUPERADMIN_PASSWORD=${SUPERADMIN_PASSWORD:-$SUPERADMIN_PASSWORD_CFG}
        DEMO_USER_PASSWORD=${DEMO_USER_PASSWORD:-$DEMO_USER_PASSWORD_CFG}


        # Defaults for JWT if not found or empty
        if [ -z "$JWT_SECRET" ]; then
            JWT_SECRET=$(generate_jwt_secret)
        fi
    else
        # Set defaults if no config file
        POSTGRES_USER="sagrafacile"
        POSTGRES_PASSWORD="sagrafacilepass"
        POSTGRES_DB="sagrafaciledb"
        JWT_SECRET=$(generate_jwt_secret)
        SAGRAFACILE_SEED_DEMO_DATA="false"
    fi
}

# --- Main Script ---
echo "Starting SagraFacile Interactive Setup..."
echo

RECONFIGURE=false
if [ -f "$CONFIG_FILE" ]; then
    echo "Existing configuration file ($CONFIG_FILE) found."
    echo "1. Use existing configuration (default)"
    echo "2. Re-configure SagraFacile"
    echo "3. Exit setup"
    read -r -p "Choose an option (1-3) [1]: " choice
    choice=${choice:-1}

    case "$choice" in
        1)
            echo "Using existing configuration."
            load_config_from_json
            RECONFIGURE=false
            ;;
        2)
            echo "Proceeding with re-configuration..."
            load_config_from_json # Load existing to provide defaults
            RECONFIGURE=true
            ;;
        3)
            echo "Exiting setup."
            exit 0
            ;;
        *)
            echo "Invalid option. Using existing configuration."
            load_config_from_json
            RECONFIGURE=false
            ;;
    esac
else
    echo "No existing configuration file ($CONFIG_FILE) found. Proceeding with initial setup."
    load_config_from_json # Load defaults
    RECONFIGURE=true
fi

if [ "$RECONFIGURE" = true ]; then
    echo
    echo "--- SagraFacile Configuration ---"
    prompt_for_input "Enter your domain name (e.g., pos.myrestaurant.com)" MY_DOMAIN "$MY_DOMAIN"
    prompt_for_input "Enter your Cloudflare API Token" CLOUDFLARE_API_TOKEN "$CLOUDFLARE_API_TOKEN"

    echo
    echo "--- Database Configuration ---"
    prompt_for_input "Enter PostgreSQL User" POSTGRES_USER "$POSTGRES_USER"
    prompt_for_input "Enter PostgreSQL Password" POSTGRES_PASSWORD "$POSTGRES_PASSWORD"
    prompt_for_input "Enter PostgreSQL Database Name" POSTGRES_DB "$POSTGRES_DB"

    echo
    echo "--- Security Configuration ---"
    prompt_for_input "Enter JWT Secret (leave blank to auto-generate)" JWT_SECRET_INPUT "$JWT_SECRET"
    if [ -z "$JWT_SECRET_INPUT" ]; then
        JWT_SECRET=$(generate_jwt_secret)
        echo "Auto-generated JWT Secret: $JWT_SECRET"
    else
        JWT_SECRET="$JWT_SECRET_INPUT"
    fi

    echo
    echo "--- Initial Data Configuration ---"
    prompt_yes_no "Seed Sagra di Tencarola demo data?" SEED_DEMO_CHOICE "${SAGRAFACILE_SEED_DEMO_DATA:-no}"
    SAGRAFACILE_SEED_DEMO_DATA=$SEED_DEMO_CHOICE

    if [ "$SAGRAFACILE_SEED_DEMO_DATA" = true ]; then
        INITIAL_ORGANIZATION_NAME=""
        INITIAL_ADMIN_EMAIL=""
        INITIAL_ADMIN_PASSWORD=""
        # Optionally prompt for DEMO_USER_PASSWORD if it's meant to be configurable here
        # prompt_for_input "Enter Demo User Password (optional, leave blank for default)" DEMO_USER_PASSWORD "$DEMO_USER_PASSWORD"
    else
        echo
        echo "--- Initial Organization & Admin User Setup ---"
        echo "(This will only be applied if no other user-defined organizations exist in the database)"
        prompt_for_input "Enter Initial Organization Name" INITIAL_ORGANIZATION_NAME "$INITIAL_ORGANIZATION_NAME"
        prompt_for_input "Enter Initial Admin Email" INITIAL_ADMIN_EMAIL "$INITIAL_ADMIN_EMAIL"
        prompt_for_input "Enter Initial Admin Password" INITIAL_ADMIN_PASSWORD "$INITIAL_ADMIN_PASSWORD"
    fi

    # Optionally prompt for SuperAdmin credentials if they should be configurable here
    # echo
    # echo "--- SuperAdmin Configuration (Optional) ---"
    # prompt_for_input "Enter SuperAdmin Email (optional, leave blank for default)" SUPERADMIN_EMAIL "$SUPERADMIN_EMAIL"
    # prompt_for_input "Enter SuperAdmin Password (optional, leave blank for default)" SUPERADMIN_PASSWORD "$SUPERADMIN_PASSWORD"

    echo
    echo "--- Advanced Configuration ---"
    prompt_yes_no "Configure advanced settings (JWT Issuer/Audience, Polling Service, etc.)?" CONFIGURE_ADVANCED "no"

    if [ "$CONFIGURE_ADVANCED" = true ]; then
        prompt_for_input "Enter JWT Issuer" JWT_ISSUER "${JWT_ISSUER:-SagraFacile}" false
        prompt_for_input "Enter JWT Audience" JWT_AUDIENCE "${JWT_AUDIENCE:-SagraFacileApp}" false
        prompt_yes_no "Enable PreOrder Polling Service?" ENABLE_PREORDER_POLLING_SERVICE_CHOICE "${ENABLE_PREORDER_POLLING_SERVICE:-yes}"
        ENABLE_PREORDER_POLLING_SERVICE=$ENABLE_PREORDER_POLLING_SERVICE_CHOICE
        prompt_for_input "Enter Cloudflare Email (optional, for some Caddy DNS plugins)" CLOUDFLARE_EMAIL "$CLOUDFLARE_EMAIL" true

        if [ "$SAGRAFACILE_SEED_DEMO_DATA" = true ]; then
            prompt_for_input "Enter Demo User Password (leave blank for default 'DemoUserPass123!')" DEMO_USER_PASSWORD_INPUT "$DEMO_USER_PASSWORD" true
            DEMO_USER_PASSWORD=${DEMO_USER_PASSWORD_INPUT:-DemoUserPass123!}
        fi
        
        prompt_for_input "Enter SuperAdmin Email (optional, leave blank for 'superadmin@example.com')" SUPERADMIN_EMAIL_INPUT "$SUPERADMIN_EMAIL" true
        SUPERADMIN_EMAIL=${SUPERADMIN_EMAIL_INPUT:-superadmin@example.com}
        prompt_for_input "Enter SuperAdmin Password (optional, leave blank for 'SuperAdminPass123!')" SUPERADMIN_PASSWORD_INPUT "$SUPERADMIN_PASSWORD" true
        SUPERADMIN_PASSWORD=${SUPERADMIN_PASSWORD_INPUT:-SuperAdminPass123!}
    else
        # Ensure defaults are set if not configuring advanced and they weren't loaded from an existing file
        JWT_ISSUER=${JWT_ISSUER:-SagraFacile}
        JWT_AUDIENCE=${JWT_AUDIENCE:-SagraFacileApp}
        ENABLE_PREORDER_POLLING_SERVICE=${ENABLE_PREORDER_POLLING_SERVICE:-true}
        # CLOUDFLARE_EMAIL can remain as loaded or empty
        SUPERADMIN_EMAIL=${SUPERADMIN_EMAIL:-superadmin@example.com}
        SUPERADMIN_PASSWORD=${SUPERADMIN_PASSWORD:-SuperAdminPass123!}
        if [ "$SAGRAFACILE_SEED_DEMO_DATA" = true ]; then
            DEMO_USER_PASSWORD=${DEMO_USER_PASSWORD:-DemoUserPass123!}
        fi
    fi

    echo
    echo "Saving configuration to $CONFIG_FILE..."
    cat > "$CONFIG_FILE" << EOF
{
  "MY_DOMAIN": "$MY_DOMAIN",
  "CLOUDFLARE_API_TOKEN": "$CLOUDFLARE_API_TOKEN",
  "POSTGRES_USER": "$POSTGRES_USER",
  "POSTGRES_PASSWORD": "$POSTGRES_PASSWORD",
  "POSTGRES_DB": "$POSTGRES_DB",
  "JWT_SECRET": "$JWT_SECRET",
  "JWT_ISSUER": "$JWT_ISSUER",
  "JWT_AUDIENCE": "$JWT_AUDIENCE",
  "SAGRAFACILE_SEED_DEMO_DATA": $SAGRAFACILE_SEED_DEMO_DATA,
  "INITIAL_ORGANIZATION_NAME": "$INITIAL_ORGANIZATION_NAME",
  "INITIAL_ADMIN_EMAIL": "$INITIAL_ADMIN_EMAIL",
  "INITIAL_ADMIN_PASSWORD": "$INITIAL_ADMIN_PASSWORD",
  "SUPERADMIN_EMAIL": "$SUPERADMIN_EMAIL",
  "SUPERADMIN_PASSWORD": "$SUPERADMIN_PASSWORD",
  "DEMO_USER_PASSWORD": "$DEMO_USER_PASSWORD",
  "ENABLE_PREORDER_POLLING_SERVICE": $ENABLE_PREORDER_POLLING_SERVICE,
  "CLOUDFLARE_EMAIL": "$CLOUDFLARE_EMAIL"
}
EOF
    if [ $? -eq 0 ]; then
        echo "Configuration saved successfully."
    else
        echo "ERROR: Failed to save configuration to $CONFIG_FILE."
        exit 1
    fi
fi

echo
echo "Generating $ENV_FILE from configuration..."
cat > "$ENV_FILE" << EOF
# This file is auto-generated by start.sh from sagrafacile_config.json
# Do not edit this file directly. Re-run start.sh to re-configure.

MY_DOMAIN=$MY_DOMAIN
CLOUDFLARE_API_TOKEN=$CLOUDFLARE_API_TOKEN

POSTGRES_USER=$POSTGRES_USER
POSTGRES_PASSWORD=$POSTGRES_PASSWORD
POSTGRES_DB=$POSTGRES_DB

JWT_SECRET=$JWT_SECRET
JWT_ISSUER=$JWT_ISSUER
JWT_AUDIENCE=$JWT_AUDIENCE

# API Configuration
CONNECTION_STRING=Host=db;Port=5432;Database=\$POSTGRES_DB;Username=\$POSTGRES_USER;Password=\$POSTGRES_PASSWORD;
ASPNETCORE_ENVIRONMENT=Development
# For production, you might set ASPNETCORE_ENVIRONMENT=Production

# Frontend Configuration
NEXT_PUBLIC_API_BASE_URL=/api

# Data Seeding Configuration
SAGRAFACILE_SEED_DEMO_DATA=$SAGRAFACILE_SEED_DEMO_DATA
INITIAL_ORGANIZATION_NAME=$INITIAL_ORGANIZATION_NAME
INITIAL_ADMIN_EMAIL=$INITIAL_ADMIN_EMAIL
INITIAL_ADMIN_PASSWORD=$INITIAL_ADMIN_PASSWORD

# Optional SuperAdmin/Demo User Passwords (sourced from sagrafacile_config.json)
SUPERADMIN_EMAIL=${SUPERADMIN_EMAIL:-superadmin@example.com}
SUPERADMIN_PASSWORD=${SUPERADMIN_PASSWORD:-SuperAdminPass123!}
DEMO_USER_PASSWORD=${DEMO_USER_PASSWORD:-DemoUserPass123!}

# Docker Compose Project Name (optional, defaults to directory name)
# COMPOSE_PROJECT_NAME=sagrafacile

# Caddy specific (already covered by MY_DOMAIN and CLOUDFLARE_API_TOKEN)
# ACME_AGREE=true # Caddy v1, not needed for v2 with direct config
CLOUDFLARE_EMAIL=$CLOUDFLARE_EMAIL

# Enable PreOrder Polling Service (true or false, defaults to true if not set)
ENABLE_PREORDER_POLLING_SERVICE=$ENABLE_PREORDER_POLLING_SERVICE
EOF

if [ $? -eq 0 ]; then
    echo "$ENV_FILE generated successfully."
else
    echo "ERROR: Failed to generate $ENV_FILE."
    exit 1
fi

echo
echo "Ensuring Docker services are running..."
docker compose up -d

echo
echo "SagraFacile services are starting up."
echo "Caddy will attempt to obtain a Let's Encrypt SSL certificate for your domain: $MY_DOMAIN"
echo "This may take a few moments, especially on the first run."
echo
echo "Once all services are running:"
echo "- You should be able to access the application at: https://$MY_DOMAIN"
echo
echo "IMPORTANT FOR LOCAL NETWORK ACCESS:"
echo "To access SagraFacile from other devices on your local network using https://$MY_DOMAIN,"
echo "you MUST configure your router's Local DNS settings to point $MY_DOMAIN"
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
exit 0

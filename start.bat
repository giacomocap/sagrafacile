@echo off
setlocal EnableDelayedExpansion

SET "CONFIG_FILE=sagrafacile_config.json"
SET "ENV_FILE=.env"

REM --- Function to generate a random JWT secret ---
:generate_jwt_secret
    SET "CHARS=ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%%^&*()_+-=[]{}|;':,./<>?"
    SET "JWT_SECRET_GEN="
    FOR /L %%N IN (1,1,32) DO (
        SET /A "RND_NUM=!RANDOM! * 70 / 32768"
        CALL SET "JWT_SECRET_GEN=!JWT_SECRET_GEN!!CHARS:~%RND_NUM%,1!"
    )
    goto :eof

REM --- Function to load config from JSON (very basic, for defaults if file exists and not reconfiguring) ---
REM --- This is simplified. Robust JSON parsing in pure batch is complex. ---
:load_config_vars_from_json
    IF NOT EXIST "%CONFIG_FILE%" (
        REM Set hardcoded defaults if config file doesn't exist (should only happen if user skips config)
        SET "MY_DOMAIN_CFG=your.domain.com"
        SET "CLOUDFLARE_API_TOKEN_CFG="
        SET "POSTGRES_USER_CFG=sagrafacile"
        SET "POSTGRES_PASSWORD_CFG=sagrafacilepass"
        SET "POSTGRES_DB_CFG=sagrafaciledb"
        CALL :generate_jwt_secret
        SET "JWT_SECRET_CFG=!JWT_SECRET_GEN!"
        SET "SAGRAFACILE_SEED_DEMO_DATA_CFG=false"
        SET "INITIAL_ORGANIZATION_NAME_CFG="
        SET "INITIAL_ADMIN_EMAIL_CFG="
        SET "INITIAL_ADMIN_PASSWORD_CFG="
        
        SET "JWT_ISSUER_CFG=SagraFacile"
        SET "JWT_AUDIENCE_CFG=SagraFacileApp"
        SET "ENABLE_PREORDER_POLLING_SERVICE_CFG=true"
        SET "CLOUDFLARE_EMAIL_CFG="
        SET "SUPERADMIN_EMAIL_CFG=superadmin@example.com"
        SET "SUPERADMIN_PASSWORD_CFG=SuperAdminPass123!"
        SET "DEMO_USER_PASSWORD_CFG=DemoUserPass123!"
        goto :eof
    )

    FOR /F "usebackq tokens=1,* delims=:" %%A IN (`findstr /C:"\"MY_DOMAIN\"" "%CONFIG_FILE%"`) DO (
        FOR /F "tokens=1 delims=," %%C IN ("%%B") DO (
            SET "MY_DOMAIN_LINE=%%C"
            SET MY_DOMAIN_LINE=!MY_DOMAIN_LINE:"=!
            SET MY_DOMAIN_LINE=!MY_DOMAIN_LINE: =!
            SET "MY_DOMAIN_CFG=!MY_DOMAIN_LINE!"
        )
    )
    FOR /F "usebackq tokens=1,* delims=:" %%A IN (`findstr /C:"\"CLOUDFLARE_API_TOKEN\"" "%CONFIG_FILE%"`) DO (
        FOR /F "tokens=1 delims=," %%C IN ("%%B") DO (
            SET "CLOUDFLARE_TOKEN_LINE=%%C"
            SET CLOUDFLARE_TOKEN_LINE=!CLOUDFLARE_TOKEN_LINE:"=!
            SET CLOUDFLARE_TOKEN_LINE=!CLOUDFLARE_TOKEN_LINE: =!
            SET "CLOUDFLARE_API_TOKEN_CFG=!CLOUDFLARE_TOKEN_LINE!"
        )
    )
    FOR /F "usebackq tokens=1,* delims=:" %%A IN (`findstr /C:"\"POSTGRES_USER\"" "%CONFIG_FILE%"`) DO (
        FOR /F "tokens=1 delims=," %%C IN ("%%B") DO (
            SET "PG_USER_LINE=%%C"
            SET PG_USER_LINE=!PG_USER_LINE:"=!
            SET PG_USER_LINE=!PG_USER_LINE: =!
            SET "POSTGRES_USER_CFG=!PG_USER_LINE!"
        )
    )
    FOR /F "usebackq tokens=1,* delims=:" %%A IN (`findstr /C:"\"POSTGRES_PASSWORD\"" "%CONFIG_FILE%"`) DO (
        FOR /F "tokens=1 delims=," %%C IN ("%%B") DO (
            SET "PG_PASS_LINE=%%C"
            SET PG_PASS_LINE=!PG_PASS_LINE:"=!
            SET PG_PASS_LINE=!PG_PASS_LINE: =!
            SET "POSTGRES_PASSWORD_CFG=!PG_PASS_LINE!"
        )
    )
    FOR /F "usebackq tokens=1,* delims=:" %%A IN (`findstr /C:"\"POSTGRES_DB\"" "%CONFIG_FILE%"`) DO (
        FOR /F "tokens=1 delims=," %%C IN ("%%B") DO (
            SET "PG_DB_LINE=%%C"
            SET PG_DB_LINE=!PG_DB_LINE:"=!
            SET PG_DB_LINE=!PG_DB_LINE: =!
            SET "POSTGRES_DB_CFG=!PG_DB_LINE!"
        )
    )
    FOR /F "usebackq tokens=1,* delims=:" %%A IN (`findstr /C:"\"JWT_SECRET\"" "%CONFIG_FILE%"`) DO (
        FOR /F "tokens=1 delims=," %%C IN ("%%B") DO (
            SET "JWT_LINE=%%C"
            SET JWT_LINE=!JWT_LINE:"=!
            SET JWT_LINE=!JWT_LINE: =!
            SET "JWT_SECRET_CFG=!JWT_LINE!"
        )
    )
    FOR /F "usebackq tokens=1,* delims=:" %%A IN (`findstr /C:"\"SAGRAFACILE_SEED_DEMO_DATA\"" "%CONFIG_FILE%"`) DO (
        FOR /F "tokens=1 delims=," %%C IN ("%%B") DO (
            SET "SEED_LINE=%%C"
            SET SEED_LINE=!SEED_LINE: =!
            SET "SAGRAFACILE_SEED_DEMO_DATA_CFG=!SEED_LINE!"
        )
    )
    FOR /F "usebackq tokens=1,* delims=:" %%A IN (`findstr /C:"\"INITIAL_ORGANIZATION_NAME\"" "%CONFIG_FILE%"`) DO (
        FOR /F "tokens=1 delims=," %%C IN ("%%B") DO (
            SET "ORG_NAME_LINE=%%C"
            SET ORG_NAME_LINE=!ORG_NAME_LINE:"=!
            SET ORG_NAME_LINE=!ORG_NAME_LINE: =!
            SET "INITIAL_ORGANIZATION_NAME_CFG=!ORG_NAME_LINE!"
        )
    )
    FOR /F "usebackq tokens=1,* delims=:" %%A IN (`findstr /C:"\"INITIAL_ADMIN_EMAIL\"" "%CONFIG_FILE%"`) DO (
        FOR /F "tokens=1 delims=," %%C IN ("%%B") DO (
            SET "ADMIN_EMAIL_LINE=%%C"
            SET ADMIN_EMAIL_LINE=!ADMIN_EMAIL_LINE:"=!
            SET ADMIN_EMAIL_LINE=!ADMIN_EMAIL_LINE: =!
            SET "INITIAL_ADMIN_EMAIL_CFG=!ADMIN_EMAIL_LINE!"
        )
    )
    FOR /F "usebackq tokens=1,* delims=:" %%A IN (`findstr /C:"\"INITIAL_ADMIN_PASSWORD\"" "%CONFIG_FILE%"`) DO (
        FOR /F "tokens=1 delims=," %%C IN ("%%B") DO (
            SET "ADMIN_PASS_LINE=%%C"
            SET ADMIN_PASS_LINE=!ADMIN_PASS_LINE:"=!
            SET ADMIN_PASS_LINE=!ADMIN_PASS_LINE: =!
            SET "INITIAL_ADMIN_PASSWORD_CFG=!ADMIN_PASS_LINE!"
        )
    )
    FOR /F "usebackq tokens=1,* delims=:" %%A IN (`findstr /C:"\"SUPERADMIN_EMAIL\"" "%CONFIG_FILE%"`) DO (
        FOR /F "tokens=1 delims=," %%C IN ("%%B") DO (
            SET "SA_EMAIL_LINE=%%C"
            SET SA_EMAIL_LINE=!SA_EMAIL_LINE:"=!
            SET SA_EMAIL_LINE=!SA_EMAIL_LINE: =!
            SET "SUPERADMIN_EMAIL_CFG=!SA_EMAIL_LINE!"
        )
    )
     FOR /F "usebackq tokens=1,* delims=:" %%A IN (`findstr /C:"\"SUPERADMIN_PASSWORD\"" "%CONFIG_FILE%"`) DO (
        FOR /F "tokens=1 delims=," %%C IN ("%%B") DO (
            SET "SA_PASS_LINE=%%C"
            SET SA_PASS_LINE=!SA_PASS_LINE:"=!
            SET SA_PASS_LINE=!SA_PASS_LINE: =!
            SET "SUPERADMIN_PASSWORD_CFG=!SA_PASS_LINE!"
        )
    )
     FOR /F "usebackq tokens=1,* delims=:" %%A IN (`findstr /C:"\"DEMO_USER_PASSWORD\"" "%CONFIG_FILE%"`) DO (
        FOR /F "tokens=1 delims=," %%C IN ("%%B") DO (
            SET "DEMO_PASS_LINE=%%C"
            SET DEMO_PASS_LINE=!DEMO_PASS_LINE:"=!
            SET DEMO_PASS_LINE=!DEMO_PASS_LINE: =!
            SET "DEMO_USER_PASSWORD_CFG=!DEMO_PASS_LINE!"
        )
    )
    FOR /F "usebackq tokens=1,* delims=:" %%A IN (`findstr /C:"\"JWT_ISSUER\"" "%CONFIG_FILE%"`) DO (
        FOR /F "tokens=1 delims=," %%C IN ("%%B") DO (
            SET "JWT_ISSUER_LINE=%%C"
            SET JWT_ISSUER_LINE=!JWT_ISSUER_LINE:"=!
            SET JWT_ISSUER_LINE=!JWT_ISSUER_LINE: =!
            SET "JWT_ISSUER_CFG=!JWT_ISSUER_LINE!"
        )
    )
    FOR /F "usebackq tokens=1,* delims=:" %%A IN (`findstr /C:"\"JWT_AUDIENCE\"" "%CONFIG_FILE%"`) DO (
        FOR /F "tokens=1 delims=," %%C IN ("%%B") DO (
            SET "JWT_AUDIENCE_LINE=%%C"
            SET JWT_AUDIENCE_LINE=!JWT_AUDIENCE_LINE:"=!
            SET JWT_AUDIENCE_LINE=!JWT_AUDIENCE_LINE: =!
            SET "JWT_AUDIENCE_CFG=!JWT_AUDIENCE_LINE!"
        )
    )
    FOR /F "usebackq tokens=1,* delims=:" %%A IN (`findstr /C:"\"ENABLE_PREORDER_POLLING_SERVICE\"" "%CONFIG_FILE%"`) DO (
        FOR /F "tokens=1 delims=," %%C IN ("%%B") DO (
            SET "POLLING_LINE=%%C"
            SET POLLING_LINE=!POLLING_LINE: =!
            SET "ENABLE_PREORDER_POLLING_SERVICE_CFG=!POLLING_LINE!"
        )
    )
    FOR /F "usebackq tokens=1,* delims=:" %%A IN (`findstr /C:"\"CLOUDFLARE_EMAIL\"" "%CONFIG_FILE%"`) DO (
        FOR /F "tokens=1 delims=," %%C IN ("%%B") DO (
            SET "CF_EMAIL_LINE=%%C"
            SET CF_EMAIL_LINE=!CF_EMAIL_LINE:"=!
            SET CF_EMAIL_LINE=!CF_EMAIL_LINE: =!
            SET "CLOUDFLARE_EMAIL_CFG=!CF_EMAIL_LINE!"
        )
    )

    REM Fallback defaults if parsing failed or values are empty
    IF "!MY_DOMAIN_CFG!"=="" SET "MY_DOMAIN_CFG=your.domain.com"
    IF "!POSTGRES_USER_CFG!"=="" SET "POSTGRES_USER_CFG=sagrafacile"
    IF "!POSTGRES_PASSWORD_CFG!"=="" SET "POSTGRES_PASSWORD_CFG=sagrafacilepass"
    IF "!POSTGRES_DB_CFG!"=="" SET "POSTGRES_DB_CFG=sagrafaciledb"
    IF "!JWT_SECRET_CFG!"=="" (
        CALL :generate_jwt_secret
        SET "JWT_SECRET_CFG=!JWT_SECRET_GEN!"
    )
    IF "!SAGRAFACILE_SEED_DEMO_DATA_CFG!"=="" SET "SAGRAFACILE_SEED_DEMO_DATA_CFG=false"
    
    IF "!JWT_ISSUER_CFG!"=="" SET "JWT_ISSUER_CFG=SagraFacile"
    IF "!JWT_AUDIENCE_CFG!"=="" SET "JWT_AUDIENCE_CFG=SagraFacileApp"
    IF "!ENABLE_PREORDER_POLLING_SERVICE_CFG!"=="" SET "ENABLE_PREORDER_POLLING_SERVICE_CFG=true"
    REM CLOUDFLARE_EMAIL_CFG can be empty
    IF "!SUPERADMIN_EMAIL_CFG!"=="" SET "SUPERADMIN_EMAIL_CFG=superadmin@example.com"
    IF "!SUPERADMIN_PASSWORD_CFG!"=="" SET "SUPERADMIN_PASSWORD_CFG=SuperAdminPass123!"
    IF "!DEMO_USER_PASSWORD_CFG!"=="" SET "DEMO_USER_PASSWORD_CFG=DemoUserPass123!"
goto :eof

REM --- Main Script ---
ECHO Starting SagraFacile Interactive Setup...
ECHO.

SET RECONFIGURE=false
IF EXIST "%CONFIG_FILE%" (
    ECHO Existing configuration file (%CONFIG_FILE%) found.
    ECHO 1. Use existing configuration (default)
    ECHO 2. Re-configure SagraFacile
    ECHO 3. Exit setup
    CHOICE /C 123 /N /M "Choose an option (1-3) [1]:"
    IF ERRORLEVEL 3 (
        ECHO Exiting setup.
        goto :eof
    )
    IF ERRORLEVEL 2 (
        ECHO Proceeding with re-configuration...
        SET RECONFIGURE=true
        CALL :load_config_vars_from_json
    )
    IF ERRORLEVEL 1 (
        ECHO Using existing configuration.
        SET RECONFIGURE=false
        CALL :load_config_vars_from_json
    )
) ELSE (
    ECHO No existing configuration file (%CONFIG_FILE%) found. Proceeding with initial setup.
    SET RECONFIGURE=true
    CALL :load_config_vars_from_json REM Load hardcoded defaults
)

IF "%RECONFIGURE%"=="true" (
    ECHO.
    ECHO --- SagraFacile Configuration ---
    SET /P "MY_DOMAIN_VAR=Enter your domain name (e.g., pos.myrestaurant.com) [%MY_DOMAIN_CFG%]: "
    IF "!MY_DOMAIN_VAR!"=="" (SET "MY_DOMAIN_VAR=!MY_DOMAIN_CFG!")

    SET /P "CLOUDFLARE_API_TOKEN_VAR=Enter your Cloudflare API Token [%CLOUDFLARE_API_TOKEN_CFG%]: "
    IF "!CLOUDFLARE_API_TOKEN_VAR!"=="" (SET "CLOUDFLARE_API_TOKEN_VAR=!CLOUDFLARE_API_TOKEN_CFG!")
    :loop_cloudflare_token
    IF "!CLOUDFLARE_API_TOKEN_VAR!"=="" (
        ECHO This field cannot be empty.
        SET /P "CLOUDFLARE_API_TOKEN_VAR=Enter your Cloudflare API Token: "
        GOTO :loop_cloudflare_token
    )


    ECHO.
    ECHO --- Database Configuration ---
    SET /P "POSTGRES_USER_VAR=Enter PostgreSQL User [%POSTGRES_USER_CFG%]: "
    IF "!POSTGRES_USER_VAR!"=="" (SET "POSTGRES_USER_VAR=!POSTGRES_USER_CFG!")

    SET /P "POSTGRES_PASSWORD_VAR=Enter PostgreSQL Password [%POSTGRES_PASSWORD_CFG%]: "
    IF "!POSTGRES_PASSWORD_VAR!"=="" (SET "POSTGRES_PASSWORD_VAR=!POSTGRES_PASSWORD_CFG!")

    SET /P "POSTGRES_DB_VAR=Enter PostgreSQL Database Name [%POSTGRES_DB_CFG%]: "
    IF "!POSTGRES_DB_VAR!"=="" (SET "POSTGRES_DB_VAR=!POSTGRES_DB_CFG!")

    ECHO.
    ECHO --- Security Configuration ---
    SET /P "JWT_SECRET_INPUT=Enter JWT Secret (leave blank to auto-generate) [%JWT_SECRET_CFG%]: "
    IF "!JWT_SECRET_INPUT!"=="" (
        IF "!JWT_SECRET_CFG!"=="" ( CALL :generate_jwt_secret & SET "JWT_SECRET_VAR=!JWT_SECRET_GEN!" ) ELSE ( SET "JWT_SECRET_VAR=!JWT_SECRET_CFG!" )
        ECHO Using JWT Secret: !JWT_SECRET_VAR!
    ) ELSE (
        SET "JWT_SECRET_VAR=!JWT_SECRET_INPUT!"
    )


    ECHO.
    ECHO --- Initial Data Configuration ---
    SET "SEED_DEMO_CHOICE_DEFAULT=N"
    IF "!SAGRAFACILE_SEED_DEMO_DATA_CFG!"=="true" SET "SEED_DEMO_CHOICE_DEFAULT=Y"
    CHOICE /C YN /N /M "Seed Sagra di Tencarola demo data? (Y/N) [%SEED_DEMO_CHOICE_DEFAULT%]:"
    IF ERRORLEVEL 2 (
        SET "SAGRAFACILE_SEED_DEMO_DATA_VAR=false"
    ) ELSE (
        SET "SAGRAFACILE_SEED_DEMO_DATA_VAR=true"
    )

    IF "!SAGRAFACILE_SEED_DEMO_DATA_VAR!"=="true" (
        SET "INITIAL_ORGANIZATION_NAME_VAR="
        SET "INITIAL_ADMIN_EMAIL_VAR="
        SET "INITIAL_ADMIN_PASSWORD_VAR="
        REM Optionally prompt for DEMO_USER_PASSWORD
        SET /P "DEMO_USER_PASSWORD_VAR=Enter Demo User Password (optional, leave blank for default) [%DEMO_USER_PASSWORD_CFG%]: "
        IF "!DEMO_USER_PASSWORD_VAR!"=="" (SET "DEMO_USER_PASSWORD_VAR=!DEMO_USER_PASSWORD_CFG!")

    ) ELSE (
        ECHO.
        ECHO --- Initial Organization ^& Admin User Setup ---
        ECHO (This will only be applied if no other user-defined organizations exist in the database)
        SET /P "INITIAL_ORGANIZATION_NAME_VAR=Enter Initial Organization Name [%INITIAL_ORGANIZATION_NAME_CFG%]: "
        IF "!INITIAL_ORGANIZATION_NAME_VAR!"=="" (SET "INITIAL_ORGANIZATION_NAME_VAR=!INITIAL_ORGANIZATION_NAME_CFG!")

        SET /P "INITIAL_ADMIN_EMAIL_VAR=Enter Initial Admin Email [%INITIAL_ADMIN_EMAIL_CFG%]: "
        IF "!INITIAL_ADMIN_EMAIL_VAR!"=="" (SET "INITIAL_ADMIN_EMAIL_VAR=!INITIAL_ADMIN_EMAIL_CFG!")

        SET /P "INITIAL_ADMIN_PASSWORD_VAR=Enter Initial Admin Password [%INITIAL_ADMIN_PASSWORD_CFG%]: "
        IF "!INITIAL_ADMIN_PASSWORD_VAR!"=="" (SET "INITIAL_ADMIN_PASSWORD_VAR=!INITIAL_ADMIN_PASSWORD_CFG!")
    )
    
    REM Preserve SuperAdmin credentials if they were loaded, or use defaults
    IF "!SUPERADMIN_EMAIL_VAR!"=="" (SET "SUPERADMIN_EMAIL_VAR=!SUPERADMIN_EMAIL_CFG!")
    IF "!SUPERADMIN_PASSWORD_VAR!"=="" (SET "SUPERADMIN_PASSWORD_VAR=!SUPERADMIN_PASSWORD_CFG!")
    IF "!DEMO_USER_PASSWORD_VAR!"=="" (SET "DEMO_USER_PASSWORD_VAR=!DEMO_USER_PASSWORD_CFG!")

    ECHO.
    ECHO --- Advanced Configuration ---
    SET "CONFIGURE_ADVANCED_DEFAULT=N"
    CHOICE /C YN /N /M "Configure advanced settings (JWT Issuer/Audience, Polling Service, etc.)? (Y/N) [%CONFIGURE_ADVANCED_DEFAULT%]:"
    IF ERRORLEVEL 2 (
        SET "CONFIGURE_ADVANCED=false"
        REM Ensure defaults for advanced settings if not configuring and not loaded
        IF "!JWT_ISSUER_VAR!"=="" SET "JWT_ISSUER_VAR=!JWT_ISSUER_CFG!"
        IF "!JWT_AUDIENCE_VAR!"=="" SET "JWT_AUDIENCE_VAR=!JWT_AUDIENCE_CFG!"
        IF "!ENABLE_PREORDER_POLLING_SERVICE_VAR!"=="" SET "ENABLE_PREORDER_POLLING_SERVICE_VAR=!ENABLE_PREORDER_POLLING_SERVICE_CFG!"
        IF "!CLOUDFLARE_EMAIL_VAR!"=="" SET "CLOUDFLARE_EMAIL_VAR=!CLOUDFLARE_EMAIL_CFG!"
        IF "!SUPERADMIN_EMAIL_VAR!"=="" SET "SUPERADMIN_EMAIL_VAR=!SUPERADMIN_EMAIL_CFG!"
        IF "!SUPERADMIN_PASSWORD_VAR!"=="" SET "SUPERADMIN_PASSWORD_VAR=!SUPERADMIN_PASSWORD_CFG!"
        IF "!SAGRAFACILE_SEED_DEMO_DATA_VAR!"=="true" (
            IF "!DEMO_USER_PASSWORD_VAR!"=="" SET "DEMO_USER_PASSWORD_VAR=!DEMO_USER_PASSWORD_CFG!"
        ) ELSE (
             REM Ensure demo password is blank if not seeding demo data and not set
            IF "!DEMO_USER_PASSWORD_VAR!"=="" SET "DEMO_USER_PASSWORD_VAR="
        )

    ) ELSE (
        SET "CONFIGURE_ADVANCED=true"
        ECHO.
        SET /P "JWT_ISSUER_INPUT=Enter JWT Issuer [!JWT_ISSUER_CFG!]: "
        IF "!JWT_ISSUER_INPUT!"=="" (SET "JWT_ISSUER_VAR=!JWT_ISSUER_CFG!") ELSE (SET "JWT_ISSUER_VAR=!JWT_ISSUER_INPUT!")

        SET /P "JWT_AUDIENCE_INPUT=Enter JWT Audience [!JWT_AUDIENCE_CFG!]: "
        IF "!JWT_AUDIENCE_INPUT!"=="" (SET "JWT_AUDIENCE_VAR=!JWT_AUDIENCE_CFG!") ELSE (SET "JWT_AUDIENCE_VAR=!JWT_AUDIENCE_INPUT!")

        SET "POLLING_DEFAULT=Y"
        IF "!ENABLE_PREORDER_POLLING_SERVICE_CFG!"=="false" SET "POLLING_DEFAULT=N"
        CHOICE /C YN /N /M "Enable PreOrder Polling Service? (Y/N) [%POLLING_DEFAULT%]:"
        IF ERRORLEVEL 2 (SET "ENABLE_PREORDER_POLLING_SERVICE_VAR=false") ELSE (SET "ENABLE_PREORDER_POLLING_SERVICE_VAR=true")

        SET /P "CLOUDFLARE_EMAIL_INPUT=Enter Cloudflare Email (optional) [!CLOUDFLARE_EMAIL_CFG!]: "
        IF "!CLOUDFLARE_EMAIL_INPUT!"=="" (SET "CLOUDFLARE_EMAIL_VAR=!CLOUDFLARE_EMAIL_CFG!") ELSE (SET "CLOUDFLARE_EMAIL_VAR=!CLOUDFLARE_EMAIL_INPUT!")
        
        IF "!SAGRAFACILE_SEED_DEMO_DATA_VAR!"=="true" (
            SET /P "DEMO_USER_PASSWORD_INPUT=Enter Demo User Password (leave blank for default 'DemoUserPass123!') [!DEMO_USER_PASSWORD_CFG!]: "
            IF "!DEMO_USER_PASSWORD_INPUT!"=="" (SET "DEMO_USER_PASSWORD_VAR=!DEMO_USER_PASSWORD_CFG!") ELSE (SET "DEMO_USER_PASSWORD_VAR=!DEMO_USER_PASSWORD_INPUT!")
        ) ELSE (
            REM Ensure demo password is blank if not seeding demo data and not set
            IF "!DEMO_USER_PASSWORD_VAR!"=="" SET "DEMO_USER_PASSWORD_VAR="
        )

        SET /P "SUPERADMIN_EMAIL_INPUT=Enter SuperAdmin Email (optional, blank for 'superadmin@example.com') [!SUPERADMIN_EMAIL_CFG!]: "
        IF "!SUPERADMIN_EMAIL_INPUT!"=="" (SET "SUPERADMIN_EMAIL_VAR=!SUPERADMIN_EMAIL_CFG!") ELSE (SET "SUPERADMIN_EMAIL_VAR=!SUPERADMIN_EMAIL_INPUT!")
        
        SET /P "SUPERADMIN_PASSWORD_INPUT=Enter SuperAdmin Password (optional, blank for 'SuperAdminPass123!') [!SUPERADMIN_PASSWORD_CFG!]: "
        IF "!SUPERADMIN_PASSWORD_INPUT!"=="" (SET "SUPERADMIN_PASSWORD_VAR=!SUPERADMIN_PASSWORD_CFG!") ELSE (SET "SUPERADMIN_PASSWORD_VAR=!SUPERADMIN_PASSWORD_INPUT!")
    )

    ECHO.
    ECHO Saving configuration to %CONFIG_FILE%...
    (
        ECHO {
        ECHO   "MY_DOMAIN": "!MY_DOMAIN_VAR!",
        ECHO   "CLOUDFLARE_API_TOKEN": "!CLOUDFLARE_API_TOKEN_VAR!",
        ECHO   "POSTGRES_USER": "!POSTGRES_USER_VAR!",
        ECHO   "POSTGRES_PASSWORD": "!POSTGRES_PASSWORD_VAR!",
        ECHO   "POSTGRES_DB": "!POSTGRES_DB_VAR!",
        ECHO   "JWT_SECRET": "!JWT_SECRET_VAR!",
        ECHO   "JWT_ISSUER": "!JWT_ISSUER_VAR!",
        ECHO   "JWT_AUDIENCE": "!JWT_AUDIENCE_VAR!",
        ECHO   "SAGRAFACILE_SEED_DEMO_DATA": !SAGRAFACILE_SEED_DEMO_DATA_VAR!,
        ECHO   "INITIAL_ORGANIZATION_NAME": "!INITIAL_ORGANIZATION_NAME_VAR!",
        ECHO   "INITIAL_ADMIN_EMAIL": "!INITIAL_ADMIN_EMAIL_VAR!",
        ECHO   "INITIAL_ADMIN_PASSWORD": "!INITIAL_ADMIN_PASSWORD_VAR!",
        ECHO   "SUPERADMIN_EMAIL": "!SUPERADMIN_EMAIL_VAR!",
        ECHO   "SUPERADMIN_PASSWORD": "!SUPERADMIN_PASSWORD_VAR!",
        ECHO   "DEMO_USER_PASSWORD": "!DEMO_USER_PASSWORD_VAR!",
        ECHO   "ENABLE_PREORDER_POLLING_SERVICE": !ENABLE_PREORDER_POLLING_SERVICE_VAR!,
        ECHO   "CLOUDFLARE_EMAIL": "!CLOUDFLARE_EMAIL_VAR!"
        ECHO }
    ) > "%CONFIG_FILE%"
    IF ERRORLEVEL 1 (
        ECHO ERROR: Failed to save configuration to %CONFIG_FILE%.
        PAUSE
        goto :eof
    )
    ECHO Configuration saved successfully.
    
    REM Update CFG vars with the new VARS for env generation
    SET "MY_DOMAIN_CFG=!MY_DOMAIN_VAR!"
    SET "CLOUDFLARE_API_TOKEN_CFG=!CLOUDFLARE_API_TOKEN_VAR!"
    SET "POSTGRES_USER_CFG=!POSTGRES_USER_VAR!"
    SET "POSTGRES_PASSWORD_CFG=!POSTGRES_PASSWORD_VAR!"
    SET "POSTGRES_DB_CFG=!POSTGRES_DB_VAR!"
    SET "JWT_SECRET_CFG=!JWT_SECRET_VAR!"
    SET "SAGRAFACILE_SEED_DEMO_DATA_CFG=!SAGRAFACILE_SEED_DEMO_DATA_VAR!"
    SET "INITIAL_ORGANIZATION_NAME_CFG=!INITIAL_ORGANIZATION_NAME_VAR!"
    SET "INITIAL_ADMIN_EMAIL_CFG=!INITIAL_ADMIN_EMAIL_VAR!"
    SET "INITIAL_ADMIN_PASSWORD_CFG=!INITIAL_ADMIN_PASSWORD_VAR!"
    SET "SUPERADMIN_EMAIL_CFG=!SUPERADMIN_EMAIL_VAR!"
    SET "SUPERADMIN_PASSWORD_CFG=!SUPERADMIN_PASSWORD_VAR!"
    SET "DEMO_USER_PASSWORD_CFG=!DEMO_USER_PASSWORD_VAR!"
    SET "JWT_ISSUER_CFG=!JWT_ISSUER_VAR!"
    SET "JWT_AUDIENCE_CFG=!JWT_AUDIENCE_VAR!"
    SET "ENABLE_PREORDER_POLLING_SERVICE_CFG=!ENABLE_PREORDER_POLLING_SERVICE_VAR!"
    SET "CLOUDFLARE_EMAIL_CFG=!CLOUDFLARE_EMAIL_VAR!"
)

ECHO.
ECHO Generating %ENV_FILE% from configuration...
(
    ECHO # This file is auto-generated by start.bat from sagrafacile_config.json
    ECHO # Do not edit this file directly. Re-run start.bat to re-configure.
    ECHO.
    ECHO MY_DOMAIN=!MY_DOMAIN_CFG!
    ECHO CLOUDFLARE_API_TOKEN=!CLOUDFLARE_API_TOKEN_CFG!
    ECHO CLOUDFLARE_EMAIL=!CLOUDFLARE_EMAIL_CFG!
    ECHO.
    ECHO POSTGRES_USER=!POSTGRES_USER_CFG!
    ECHO POSTGRES_PASSWORD=!POSTGRES_PASSWORD_CFG!
    ECHO POSTGRES_DB=!POSTGRES_DB_CFG!
    ECHO.
    ECHO JWT_SECRET=!JWT_SECRET_CFG!
    ECHO JWT_ISSUER=!JWT_ISSUER_CFG!
    ECHO JWT_AUDIENCE=!JWT_AUDIENCE_CFG!
    ECHO.
    ECHO # API Configuration
    ECHO CONNECTION_STRING=Host=db;Port=5432;Database=!POSTGRES_DB_CFG!;Username=!POSTGRES_USER_CFG!;Password=!POSTGRES_PASSWORD_CFG!;
    ECHO ASPNETCORE_ENVIRONMENT=Development
    ECHO # For production, you might set ASPNETCORE_ENVIRONMENT=Production
    ECHO.
    ECHO # Frontend Configuration
    ECHO NEXT_PUBLIC_API_BASE_URL=/api
    ECHO.
    ECHO # Data Seeding Configuration
    ECHO SAGRAFACILE_SEED_DEMO_DATA=!SAGRAFACILE_SEED_DEMO_DATA_CFG!
    ECHO INITIAL_ORGANIZATION_NAME=!INITIAL_ORGANIZATION_NAME_CFG!
    ECHO INITIAL_ADMIN_EMAIL=!INITIAL_ADMIN_EMAIL_CFG!
    ECHO INITIAL_ADMIN_PASSWORD=!INITIAL_ADMIN_PASSWORD_CFG!
    ECHO.
    ECHO # Optional SuperAdmin/Demo User Passwords (sourced from sagrafacile_config.json)
    ECHO SUPERADMIN_EMAIL=!SUPERADMIN_EMAIL_CFG!
    ECHO SUPERADMIN_PASSWORD=!SUPERADMIN_PASSWORD_CFG!
    ECHO DEMO_USER_PASSWORD=!DEMO_USER_PASSWORD_CFG!
    ECHO.
    ECHO # Docker Compose Project Name (optional, defaults to directory name)
    ECHO # COMPOSE_PROJECT_NAME=sagrafacile
    ECHO.
    ECHO # Enable PreOrder Polling Service (true or false, defaults to true if not set)
    ECHO ENABLE_PREORDER_POLLING_SERVICE=!ENABLE_PREORDER_POLLING_SERVICE_CFG!
) > "%ENV_FILE%"

IF ERRORLEVEL 1 (
    ECHO ERROR: Failed to generate %ENV_FILE%.
    PAUSE
    goto :eof
)
ECHO %ENV_FILE% generated successfully.

ECHO.
ECHO Ensuring Docker services are running...
docker compose up -d

ECHO.
ECHO SagraFacile services are starting up.
ECHO Caddy will attempt to obtain a Let's Encrypt SSL certificate for your domain: !MY_DOMAIN_CFG!
ECHO This may take a few moments, especially on the first run.
ECHO.
ECHO Once all services are running:
ECHO - You should be able to access the application at: https://!MY_DOMAIN_CFG!
ECHO.
ECHO IMPORTANT FOR LOCAL NETWORK ACCESS:
ECHO To access SagraFacile from other devices on your local network using https://!MY_DOMAIN_CFG!,
ECHO you MUST configure your router's Local DNS settings to point !MY_DOMAIN_CFG!
ECHO to the local IP address of this server (e.g., 192.168.1.50).
ECHO Detailed instructions are in the README.md file.
ECHO.
ECHO If Caddy fails to obtain a certificate, check its logs: docker compose logs -f caddy
ECHO Ensure your domain is correctly pointing to your public IP and Cloudflare API token is valid.
ECHO.
ECHO To view all logs, run: docker compose logs -f
ECHO To stop services, run: stop.bat
ECHO.
ECHO Press any key to exit this script (services will continue running in the background).
PAUSE
goto :eof

# SagraFacile Android Wrapper App

This Android application serves as a lightweight native wrapper for the SagraFacile Progressive Web App (PWA). Its primary function is to enable Android users to connect to a locally hosted SagraFacile server using its domain name and a trusted SSL certificate, without needing to modify device-wide DNS settings.

## Overview

The app embeds the SagraFacile PWA within an Android WebView. It internally handles network request routing: when the user configures the app with the SagraFacile server's local IP address and its public domain name, the app intercepts requests to this domain and transparently redirects them to the local IP. Crucially, it preserves the original `Host` header, allowing the SagraFacile server (Caddy) to serve the correct SSL certificate, ensuring a secure HTTPS connection.

## Features

*   **WebView Wrapper:** Displays the SagraFacile PWA in a full-screen WebView.
*   **Local DNS Resolution:** Allows use of the SagraFacile server's domain name (e.g., `https://your.domain.com`) on the local network by internally mapping it to a user-configured local IP address.
*   **SSL/TLS Support:** Works with the SSL certificate provided by the SagraFacile server, ensuring secure communication.
*   **Settings Configuration:**
    *   Users can input and save the SagraFacile server's local IP address and domain name.
    *   Settings are accessible via a menu item for updates.
*   **Splash Screen:** Displays a splash screen on app launch.
*   **Modern Back Button Handling:** Supports standard back navigation within the WebView.

## Prerequisites for Use

*   A running SagraFacile server instance on your local network, accessible via a domain name with a valid SSL certificate (typically managed by Caddy).
*   The local IP address of the SagraFacile server.
*   The domain name used by the SagraFacile server (e.g., `pos.yourdomain.com`).

## How to Use

1.  **Installation:**
    *   Download the `SagraFacileApp-release.apk` file.
    *   Transfer the APK to your Android device.
    *   Open the APK file on your Android device to install it. You may need to enable "Install from unknown sources" in your device settings.

2.  **First-Time Setup:**
    *   On the first launch, the app will display a "Please Configure Server" message.
    *   Tap the menu icon (three dots) in the toolbar and select "Settings".
    *   Enter the **SagraFacile Server Domain** (e.g., `pos.yourdomain.com`).
    *   Enter the **SagraFacile Server Local IP** (e.g., `192.168.1.100`).
    *   Tap "Save Settings".
    *   The app will automatically try to load the SagraFacile PWA from the configured domain.

3.  **Normal Use:**
    *   After the initial setup, the app will directly load the SagraFacile PWA on subsequent launches.
    *   If you need to change the server IP or domain, access the "Settings" menu item from the toolbar.

## Building from Source (for Developers)

1.  **Clone the Repository:**
    ```bash
    git clone https://github.com/your-username/sagrafacile.git # Replace with actual repo URL
    cd sagrafacile/sagrafacile-androidapp
    ```
2.  **Open in Android Studio:**
    *   Open Android Studio.
    *   Select "Open an Existing Project" and navigate to the `sagrafacile-androidapp` directory.
3.  **Configure JDK (if needed):** Ensure Android Studio is configured with a compatible JDK (usually prompted by Android Studio).
4.  **Build the Project:**
    *   Use Android Studio's build options (Build > Make Project).
    *   To generate a release APK: Build > Generate Signed Bundle / APK... (Follow the on-screen instructions to create or use a signing key).

## Technical Details

*   **Language:** Kotlin
*   **Core Components:** Android WebView, SharedPreferences (for settings), Custom WebViewClient (for request interception and redirection).
*   **Architecture Document:** For more in-depth architectural details, refer to `../../docs/AndroidWrapperAppArchitecture.md`.
*   **Project Memory:** Development notes and session summaries are in `ProjectMemory.md`.

## Troubleshooting

*   **"Webpage not available" or SSL errors:**
    *   Verify the Server Domain and Local IP are correctly entered in the app's Settings.
    *   Ensure your SagraFacile server is running and accessible on your local network at the specified IP and domain.
    *   Check that the SagraFacile server's SSL certificate is valid and trusted.
    *   Ensure your Android device is connected to the same local network as the SagraFacile server.
*   **Settings menu not visible:**
    *   The settings menu is part of the toolbar. If the PWA loads in a way that hides the native toolbar, or if an error page is shown without the toolbar, ensure the app version is up-to-date as this was addressed.

This README provides guidance for using and developing the SagraFacile Android Wrapper App.

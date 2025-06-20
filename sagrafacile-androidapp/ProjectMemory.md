# Project Memory - SagraFacile Android Wrapper App

## How to work on the project
*   **Technology:** Native Android (Kotlin), Android Studio.
*   **Core Components:** WebView, SharedPreferences, Custom WebViewClient.
*   **Purpose:** Embed the SagraFacile PWA, handling local DNS resolution internally via user-configured IP and domain.
*   **Memory:** Update this `ProjectMemory.md` file at the end of each session.

---
# Session Summaries (Newest First)

## (2025-06-20) - Initial Android Wrapper App Setup & Core Functionality
*   **Context:** Began development of the Android wrapper app as outlined in `docs/AndroidWrapperAppArchitecture.md` and `Roadmap.md` (Phase 10).
*   **Accomplishments:**
    *   **Project Setup:**
        *   Created new Android project `sagrafacile-androidapp` using Kotlin and Android Studio.
        *   Installed JDK and configured it for Android development.
    *   **Core Wrapper Implementation:**
        *   Added `INTERNET` permission to `AndroidManifest.xml`.
        *   Integrated a `WebView` into `MainActivity`.
        *   Implemented `SettingsActivity` to allow users to input and save the SagraFacile server's local IP address and domain name using `SharedPreferences`.
        *   Modified `MainActivity` to:
            *   Launch `SettingsActivity` if IP/domain are not set.
            *   Load saved IP/domain.
            *   Implement a `CustomWebViewClient` that overrides `shouldInterceptRequest`. This client:
                *   Intercepts requests to the configured SagraFacile domain.
                *   Re-routes these requests to the configured local IP address.
                *   Sets the `Host` HTTP header to the original SagraFacile domain to ensure correct SSL certificate validation by the server (Caddy).
            *   Load the SagraFacile PWA using the configured settings.
        *   Updated `MainActivity` to use `OnBackPressedDispatcher` for modern back button handling within the WebView.
    *   **App Icon:**
        *   Successfully added a custom app icon using Android Studio's Image Asset Studio, based on `images/icon.png` (assumed from user confirmation).
*   **Outcome:** The Android wrapper app is functional. It correctly prompts for server IP and domain on first launch, saves these settings, and then loads the SagraFacile PWA by internally redirecting requests to the local server IP while maintaining SSL. The app icon is also updated.
*   **Next Steps (from `docs/AndroidWrapperAppArchitecture.md` and discussion):**
    *   Implement a Splash Screen.
    *   Add a menu item in `MainActivity` to re-open `SettingsActivity`.
    *   Enhance error handling in `CustomWebViewClient` (e.g., display a custom error page).
    *   Review and confirm the application name (`app_name` in `strings.xml`).
    *   Prepare for build and distribution (signed release APK).
    *   Update project documentation (README for the Android app).

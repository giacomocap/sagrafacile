# Project Memory - SagraFacile Android Wrapper App

## How to work on the project
*   **Technology:** Native Android (Kotlin), Android Studio.
*   **Core Components:** WebView, SharedPreferences, Custom WebViewClient.
*   **Purpose:** Embed the SagraFacile PWA, handling local DNS resolution internally via user-configured IP and domain.
*   **Memory:** Update this `ProjectMemory.md` file at the end of each session.

---
# Session Summaries (Newest First)

## (2025-06-20) - Android Wrapper App Enhancements
*   **Context:** Continued development of the Android wrapper app, focusing on usability and robustness enhancements based on the next steps from the previous session and `Roadmap.md` (Phase 10).
*   **Accomplishments:**
    *   **Splash Screen:**
        *   Created `activity_splash.xml` layout displaying the app icon.
        *   Implemented `SplashActivity.kt` to show the splash screen for 1.5 seconds before launching `MainActivity`.
        *   Updated `AndroidManifest.xml` to set `SplashActivity` as the launcher activity.
        *   Corrected `AndroidManifest.xml` to use the existing `Theme.SagraFacile` for `SplashActivity` (which is already NoActionBar) to resolve a build error.
    *   **Settings Menu Item:**
        *   Created `main_menu.xml` with a "Settings" menu item.
        *   Modified `MainActivity.kt` to inflate this menu and handle item selection to launch `SettingsActivity`, allowing users to re-access settings after initial setup.
    *   **Error Handling:**
        *   Initially implemented custom error page loading (`network_error.html`) for `IOException` in `CustomWebViewClient`.
        *   User subsequently removed the custom error page loading from `MainActivity.kt`, so the app now relies on the default WebView error display if a connection fails during interception. Logging for `IOException` remains.
    *   **App Name Confirmation:**
        *   Reviewed `strings.xml` and confirmed `app_name` is "SagraFacile", which is appropriate.
    *   **Toolbar for Settings Access on Error:**
        *   Added a `Toolbar` to `activity_main.xml`.
        *   Modified `MainActivity.kt` to set this `Toolbar` as the support action bar. This ensures the "Settings" menu item is accessible even if the WebView displays the `network_error.html` page, addressing an issue where settings were unreachable on error.
    *   **Refined Initial Loading Logic:**
        *   Created `please_configure.html` in assets.
        *   Modified `MainActivity.kt`'s `loadSettingsAndLaunch()`: if IP/domain settings are missing, it now loads `please_configure.html` (using a default `WebViewClient`) instead of directly launching `SettingsActivity`. Users are instructed to use the Toolbar menu to access Settings. This provides a clearer separation of concerns and a more explicit setup flow.
    *   **Authentication Support:** Confirmed that the current WebView setup (with JavaScript, DOM Storage, and header forwarding) supports standard JWT and cookie-based authentication.
*   **Outcome:** The Android wrapper app now has a splash screen, a more robust way to access settings, relies on default WebView error pages, supports standard web authentication, and has a clearer initial configuration flow.
*   **Next Steps (from `docs/AndroidWrapperAppArchitecture.md` and `Roadmap.md`):**
    *   Prepare for build and distribution (signed release APK). *(User/Dev action in Android Studio)*
    *   Update project documentation (README for the Android app, and main `README.md` with instructions for using the Android Wrapper App). *(User/Dev action)*

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

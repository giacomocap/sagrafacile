# SagraFacile Android Wrapper App Architecture

## 1. Overview

The SagraFacile Android Wrapper App is a lightweight native Android application designed to provide a seamless and secure experience for Android users accessing the SagraFacile Progressive Web App (PWA) on a local network.

Its primary purpose is to overcome potential difficulties Android users might face in configuring device-level DNS settings to resolve the SagraFacile server's custom domain name (e.g., `https://your.domain.com`) to its local IP address.

The app embeds the SagraFacile PWA within an Android WebView component and internally handles the necessary network request modifications to ensure the PWA loads correctly using the server's trusted SSL certificate.

## 2. Goals

*   Allow Android users to access the SagraFacile PWA using the configured domain name (e.g., `https://your.domain.com`) without manual DNS changes on their device.
*   Ensure a secure HTTPS connection using the SSL certificate provided by the SagraFacile server (via Caddy).
*   Provide a user experience that feels like a dedicated application.
*   Maintain the full functionality of the SagraFacile PWA.
*   Minimize native Android development complexity, leveraging the existing web application as much as possible.

## 3. Core Technology

*   **Development Language:** Kotlin (preferred for modern Android development).
*   **IDE:** Android Studio.
*   **Primary Component:** Android WebView.
*   **Configuration Storage:** Android SharedPreferences (for storing the server's local IP address).
*   **Alternative (Consideration):** Capacitor could be explored as a framework to build the wrapper, potentially simplifying some aspects if its network interception capabilities are suitable. Initial plan leans towards native Kotlin for direct control over WebViewClient.

## 4. Key Architectural Components

### 4.1. Main Application Activity
*   Hosts the WebView component.
*   Manages the lifecycle of the WebView.
*   Loads the SagraFacile PWA URL (e.g., `https://your.domain.com`).

### 4.2. WebView Component
*   Configured to enable JavaScript, DOM storage, and other PWA-necessary features.
*   Renders the SagraFacile PWA.

### 4.3. Custom WebViewClient (Critical Component)
*   **Purpose:** To intercept network requests made by the WebView and modify them for local network access.
*   **Method Override:** `shouldInterceptRequest(WebView view, WebResourceRequest request)`
*   **Logic:**
    1.  Check if the `request.getUrl().getHost()` matches the SagraFacile domain (e.g., `your.domain.com`).
    2.  If it matches:
        a.  Retrieve the user-configured local IP address of the SagraFacile server from SharedPreferences.
        b.  Construct a new URL using the scheme (`https`), the local IP address, the original port (if specified, usually 443 for HTTPS), and the original path and query parameters from `request.getUrl()`.
        c.  Open a new `HttpURLConnection` (or similar HTTP client like OkHttp) to this IP-based URL.
        d.  **Crucially, set the `Host` HTTP header of this new connection to the original SagraFacile domain (e.g., `your.domain.com`).** This ensures Caddy serves the correct site and the SSL certificate is validated correctly.
        e.  Execute the request.
        f.  Package the response (input stream, MIME type, encoding, headers, status code) into a `WebResourceResponse` object and return it. This response is then used by the WebView.
    3.  If the host does not match (e.g., requests to external CDNs or APIs if the PWA uses any), let the WebView handle the request normally by returning `super.shouldInterceptRequest(view, request)` or `null`.

### 4.4. Settings Activity/Fragment
*   **Purpose:** Allows the user to input and save the local IP address of the SagraFacile server.
*   **UI:** A simple screen with an `EditText` for the IP address and a "Save" button.
*   **Storage:** Uses `SharedPreferences` to persist the IP address.
*   **Flow:**
    *   On first app launch, if no IP is stored, navigate the user to this settings screen.
    *   Provide an option (e.g., in an app menu) to access this screen later to update the IP.

### 4.5. Application Manifest (`AndroidManifest.xml`)
*   Declare necessary permissions:
    *   `<uses-permission android:name="android.permission.INTERNET" />`
*   Define activities (Main, Settings).
*   Specify application icon, label, theme.
*   Potentially configure `android:usesCleartextTraffic="false"` to enforce HTTPS, though the WebViewClient logic should ensure this.

## 5. User Flow

1.  **First Launch:**
    *   App checks SharedPreferences for a saved server IP.
    *   If not found, user is directed to the Settings screen.
    *   User enters the SagraFacile server's local IP (e.g., `192.168.1.100`) and saves it.
2.  **Subsequent Launches:**
    *   App loads the saved server IP.
    *   Main Activity launches, WebView initializes.
    *   WebView attempts to load `https://your.domain.com`.
3.  **Request Handling:**
    *   Custom `WebViewClient` intercepts the request.
    *   DNS override logic re-routes the request to `https://<saved_local_IP>/<original_path_query>` while setting the `Host` header to `your.domain.com`.
    *   SagraFacile server (Caddy) receives the request, validates the `Host` header, and serves content with the correct SSL certificate.
    *   WebView renders the PWA content securely.
4.  **Using the App:**
    *   User interacts with the SagraFacile PWA as they would in a standard browser.
    *   All PWA features (login, API calls, SignalR) function through the WebView.

## 6. SSL/TLS Handling

*   The SSL certificate is served by Caddy for `your.domain.com`.
*   Because the `Host` header in the modified requests matches the certificate's common name (CN) or subject alternative name (SAN), the Android system and WebView should trust the certificate without issues, provided it's a valid certificate (e.g., from Let's Encrypt).
*   No self-signed certificates or manual certificate trust on the client-side should be necessary.

## 7. Build and Distribution

*   Build a signed release APK using Android Studio.
*   Distribute the APK directly (e.g., via download from the SagraFacile project website or a shared link for event staff). Not intended for public app stores initially.

## 8. Future Considerations (Optional)

*   **QR Code for Server Configuration:** Allow scanning a QR code that contains the server domain and local IP to simplify setup.
*   **Native Enhancements:** If specific native device features are beneficial (e.g., more robust background syncing, native notifications beyond PWA capabilities), they could be added later.
*   **Error Handling:** Improved error messages if the server IP is incorrect or the server is unreachable.
*   **Automatic IP Discovery (Advanced):** Explore mDNS/Bonjour for discovering the SagraFacile server on the local network, though this adds complexity.

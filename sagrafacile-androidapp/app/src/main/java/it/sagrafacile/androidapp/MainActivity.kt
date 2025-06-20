package it.sagrafacile.androidapp

import android.content.Context
import android.content.Intent
import android.os.Bundle
import android.util.Log
import android.view.Menu
import android.view.MenuItem
import androidx.activity.OnBackPressedCallback
import android.webkit.WebResourceRequest
import android.webkit.WebResourceResponse
import android.webkit.WebView
import android.webkit.WebViewClient
import androidx.activity.enableEdgeToEdge
import androidx.appcompat.app.AppCompatActivity
import androidx.appcompat.widget.Toolbar
import androidx.core.view.ViewCompat
import androidx.core.view.WindowInsetsCompat
import java.io.IOException
import java.net.HttpURLConnection
import java.net.URL

class MainActivity : AppCompatActivity() {
    private lateinit var webView: WebView
    private var sagraFacileServerIp: String? = null
    private var sagraFacileDomain: String? = null
    private var sagraFacileBaseUrl: String? = null

    companion object {
        private const val REQUEST_CODE_SETTINGS = 1001
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContentView(R.layout.activity_main)

        val toolbar: Toolbar = findViewById(R.id.toolbar)
        setSupportActionBar(toolbar)

        webView = findViewById(R.id.webView)
        setupWebView()

        loadSettingsAndLaunch()

        ViewCompat.setOnApplyWindowInsetsListener(findViewById(R.id.main)) { v, insets ->
            val systemBars = insets.getInsets(WindowInsetsCompat.Type.systemBars())
            // Adjust padding for the main container, WebView will be a child
            v.setPadding(systemBars.left, systemBars.top, systemBars.right, systemBars.bottom)
            insets
        }

        // Handle back button press to navigate WebView history using OnBackPressedDispatcher
        onBackPressedDispatcher.addCallback(this, object : OnBackPressedCallback(true) {
            override fun handleOnBackPressed() {
                if (webView.canGoBack()) {
                    webView.goBack()
                } else {
                    // If WebView can't go back, and this callback is enabled,
                    // it means we're at the start of the WebView history.
                    // To allow the default system back behavior (e.g., exit app),
                    // we disable this callback and trigger the back press again.
                    isEnabled = false // Disable this callback
                    onBackPressedDispatcher.onBackPressed() // Trigger default back behavior
                }
            }
        })
    }

    override fun onCreateOptionsMenu(menu: Menu?): Boolean {
        menuInflater.inflate(R.menu.main_menu, menu)
        return true
    }

    override fun onOptionsItemSelected(item: MenuItem): Boolean {
        return when (item.itemId) {
            R.id.action_settings -> {
                val intent = Intent(this, SettingsActivity::class.java)
                startActivityForResult(intent, REQUEST_CODE_SETTINGS)
                true
            }
            else -> super.onOptionsItemSelected(item)
        }
    }

    private fun setupWebView() {
        webView.settings.javaScriptEnabled = true
        webView.settings.domStorageEnabled = true
        // Add other necessary settings like mixed content handling if needed
        // webView.settings.mixedContentMode = WebSettings.MIXED_CONTENT_ALWAYS_ALLOW
    }

    private fun loadSettingsAndLaunch() {
        val prefs = getSharedPreferences(SettingsActivity.PREFS_NAME, Context.MODE_PRIVATE)
        sagraFacileServerIp = prefs.getString(SettingsActivity.KEY_SERVER_IP, null)
        sagraFacileDomain = prefs.getString(SettingsActivity.KEY_SERVER_DOMAIN, null)

        if (sagraFacileServerIp.isNullOrEmpty() || sagraFacileDomain.isNullOrEmpty()) {
            // IP or Domain not saved, load a local HTML page instructing user to configure settings
            // Ensure CustomWebViewClient is NOT set here, so it doesn't try to intercept this local asset load
            webView.webViewClient = WebViewClient() // Use a default client for local assets
            webView.loadUrl("file:///android_asset/please_configure.html")
        } else {
            // Both IP and Domain are saved
            sagraFacileBaseUrl = "https://$sagraFacileDomain"
            webView.webViewClient = CustomWebViewClient()
            // Now load the actual SagraFacile URL
            webView.loadUrl(sagraFacileBaseUrl!!)
        }
    }

    override fun onActivityResult(requestCode: Int, resultCode: Int, data: Intent?) {
        super.onActivityResult(requestCode, resultCode, data)
        if (requestCode == REQUEST_CODE_SETTINGS) {
            // User has returned from SettingsActivity, reload settings and attempt to launch
            loadSettingsAndLaunch()
        }
    }

    private inner class CustomWebViewClient : WebViewClient() {
        override fun shouldInterceptRequest(view: WebView?, request: WebResourceRequest?): WebResourceResponse? {
            val requestUrl = request?.url?.toString() ?: return super.shouldInterceptRequest(view, request)

            // Log.d("WebViewDebug", "Intercepting: $requestUrl") // For debugging

            if (sagraFacileBaseUrl != null && requestUrl.startsWith(sagraFacileBaseUrl!!) && !sagraFacileServerIp.isNullOrEmpty() && sagraFacileDomain != null) {
                try {
                    val originalHost = sagraFacileDomain!! // Use the configured domain
                    val modifiedUrlString = requestUrl.replaceFirst(originalHost, sagraFacileServerIp!!)
                    val modifiedUrl = URL(modifiedUrlString)

                    // Log.d("WebViewDebug", "Modified URL: $modifiedUrl") // For debugging

                    val connection = modifiedUrl.openConnection() as HttpURLConnection
                    connection.requestMethod = request.method
                    request.requestHeaders.forEach { (key, value) ->
                        connection.setRequestProperty(key, value)
                    }
                    // CRITICAL: Set the Host header to the original domain for SSL validation
                    connection.setRequestProperty("Host", originalHost)

                    // Handle POST/PUT body if present (simplified example)
                    // For a full solution, you'd need to read the request body if it's a POST/PUT
                    // and write it to the connection. This is complex with shouldInterceptRequest.
                    // OkHttp or other libraries might offer better ways if complex body handling is needed.

                    connection.connect() // Explicitly connect

                    val statusCode = connection.responseCode
                    val reasonPhrase = connection.responseMessage ?: "" // Get reason phrase

                    // Log.d("WebViewDebug", "Response Code: $statusCode $reasonPhrase") // For debugging

                    // Get headers from the HttpURLConnection response
                    val responseHeaders = mutableMapOf<String, String>()
                    connection.headerFields.forEach { (key, values) ->
                        if (key != null && values.isNotEmpty()) {
                            responseHeaders[key] = values.joinToString(", ")
                        }
                    }
                     // Ensure "content-type" is lowercase for consistency
                    val contentType = responseHeaders.entries.firstOrNull { it.key.equals("content-type", ignoreCase = true) }?.value
                    val encoding = connection.contentEncoding ?: "UTF-8"


                    return WebResourceResponse(
                        contentType?.substringBefore(';'), // MIME type
                        encoding, // Encoding
                        statusCode,
                        reasonPhrase,
                        responseHeaders,
                        connection.inputStream
                    )
                } catch (e: IOException) {
                    Log.e("CustomWebViewClient", "Error intercepting request: ${requestUrl}", e)
                    // Load a local error page from assets
                    // Ensure the webView is accessed on the UI thread if modification is needed
                    // For just returning a response, this should be fine.
                    // However, if webView.loadUrl is called, it must be on UI thread.
                    // Here, we are returning a WebResourceResponse, which is part of the background processing of the request.
//                    try {
//                        val assetManager = applicationContext.assets
//                        val inputStream = assetManager.open("network_error.html")
//                        return WebResourceResponse(
//                            "text/html",
//                            "utf-8",
//                            inputStream
//                        )
//                    } catch (assetError: IOException) {
//                        Log.e("CustomWebViewClient", "Error loading local error page from assets", assetError)
//                        // Fallback if local error page also fails to load
//                        return WebResourceResponse(
//                            "text/plain",
//                            "utf-8",
//                            500,
//                            "Internal Server Error",
//                            mutableMapOf(),
//                            "Error loading error page.".byteInputStream()
//                        )
//                    }
                }
            }
            return super.shouldInterceptRequest(view, request)
        }

        // Optional: Handle SSL errors if you were using self-signed certs (not recommended for production)
        // override fun onReceivedSslError(view: WebView?, handler: SslErrorHandler?, error: SslError?) {
        //     handler?.proceed() // DANGEROUS: Ignores SSL errors. Only for specific local dev scenarios.
        // }
    }
}

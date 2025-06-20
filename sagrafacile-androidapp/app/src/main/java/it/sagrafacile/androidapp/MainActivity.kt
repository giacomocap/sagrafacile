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
import java.net.URL
import javax.net.ssl.HttpsURLConnection
import javax.net.ssl.HostnameVerifier
import javax.net.ssl.SSLContext
import javax.net.ssl.SSLException
import javax.net.ssl.SSLParameters
import javax.net.ssl.SSLSocket
import javax.net.ssl.SSLSocketFactory
import java.net.InetAddress // For SSLSocketFactory
import android.os.Build // For SNI handling

// OkHttp Imports
import okhttp3.OkHttpClient
import okhttp3.Request as OkHttpRequest // Alias to avoid confusion with WebResourceRequest
import okhttp3.RequestBody
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.MediaType.Companion.toMediaTypeOrNull
import okhttp3.Headers
import okhttp3.Response as OkHttpResponse // Alias
import okhttp3.logging.HttpLoggingInterceptor
import java.util.concurrent.TimeUnit

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
            // Pass the confirmed non-null sagraFacileDomain to the CustomWebViewClient
            webView.webViewClient = CustomWebViewClient(sagraFacileDomain!!)
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

    // Pass sagraFacileDomain to CustomWebViewClient constructor
    private inner class CustomWebViewClient(private val clientSagraFacileDomain: String) : WebViewClient() {
        private val okHttpClient: OkHttpClient

        init {
            val loggingInterceptor = HttpLoggingInterceptor(object : HttpLoggingInterceptor.Logger {
                override fun log(message: String) {
                    Log.d("OkHttp", message)
                }
            }).apply {
                level = HttpLoggingInterceptor.Level.BODY // Log request and response lines and their respective headers and bodies (if present).
            }

            // SNISSLSocketFactory needs the sniHostname, which is sagraFacileDomain
            // We need to ensure sagraFacileDomain is available when OkHttpClient is initialized.
            // This might require passing it to CustomWebViewClient or accessing it carefully.
            // For now, assuming sagraFacileDomain is accessible or will be handled.
            // A placeholder is used if sagraFacileDomain is null during init, though this should be avoided.
            // val currentSagraFacileDomain = sagraFacileDomain ?: "placeholder.domain.com" // Fallback, should be improved
            // Use the passed-in clientSagraFacileDomain for SNISSLSocketFactory
            okHttpClient = OkHttpClient.Builder()
                .sslSocketFactory(SNISSLSocketFactory(clientSagraFacileDomain), TrustManagerUtils.getTrustAllCerts()[0] as javax.net.ssl.X509TrustManager)
                .hostnameVerifier { _, sslSession ->
                    // Verify against the original host (clientSagraFacileDomain)
                    HttpsURLConnection.getDefaultHostnameVerifier().verify(clientSagraFacileDomain, sslSession)
                }
                .addInterceptor(loggingInterceptor)
                .connectTimeout(30, TimeUnit.SECONDS)
                .readTimeout(30, TimeUnit.SECONDS)
                .writeTimeout(30, TimeUnit.SECONDS)
                .build()
            Log.d("CustomWebViewClient", "OkHttpClient initialized with SNISSLSocketFactory for domain: $clientSagraFacileDomain")
        }


        override fun shouldInterceptRequest(view: WebView?, request: WebResourceRequest?): WebResourceResponse? {
            val requestUrl = request?.url?.toString() ?: return super.shouldInterceptRequest(view, request)

            Log.d("WebViewDebug", "Intercepting with OkHttp: $requestUrl")

            // Use clientSagraFacileDomain from constructor for checks and operations
            // sagraFacileBaseUrl is still from MainActivity for the initial check
            if (sagraFacileBaseUrl != null && requestUrl.startsWith(sagraFacileBaseUrl!!) && !sagraFacileServerIp.isNullOrEmpty()) { // Removed isInitialized check
                try {
                    val originalHost = clientSagraFacileDomain // Use domain from constructor
                    val modifiedUrlString = requestUrl.replaceFirst(originalHost, sagraFacileServerIp!!)

                    Log.d("WebViewDebug", "OkHttp - Modified URL String: $modifiedUrlString, Original Host: $originalHost")

                    val okHttpRequestBuilder = OkHttpRequest.Builder().url(modifiedUrlString)

                    // Copy headers from WebResourceRequest to OkHttpRequest
                    request.requestHeaders.forEach { (key, value) ->
                        // OkHttp's Host header is managed automatically based on the URL,
                        // but for SNI with IP-based connections, we might need to ensure it's correctly handled
                        // by the SSLSocketFactory and HostnameVerifier.
                        // Explicitly setting Host here might be overridden or conflict.
                        // The critical part is that SNISSLSocketFactory uses originalHost.
                        if (!key.equals("Host", ignoreCase = true)) { // Avoid explicitly setting Host if OkHttp handles it
                           okHttpRequestBuilder.addHeader(key, value)
                        }
                    }
                    // Ensure the Host header is set for the request itself if needed, though SNI is at SSL layer
                    okHttpRequestBuilder.header("Host", originalHost)


                    // Handle request body for POST/PUT
                    var okHttpRequestBody: RequestBody? = null
                    if (request.method == "POST" || request.method == "PUT") {
                        // This part is complex with shouldInterceptRequest as direct access to request body is not provided.
                        // WebView doesn't easily expose the request body stream here.
                        // This is a known limitation. For GET requests or simple POSTs without bodies, it's fine.
                        // For POSTs with bodies, a more advanced setup or library might be needed,
                        // or one might have to assume no body or specific content types.
                        // For now, we'll assume no body or that it's not critical for this PWA's initial load.
                        // If POSTs with bodies are failing, this is the area to investigate.
                        Log.w("WebViewDebug", "OkHttp - ${request.method} request: Body handling is limited in shouldInterceptRequest.")
                        // Example: creating an empty body if none, or a placeholder
                        // val emptyBody = "".toRequestBody("text/plain".toMediaTypeOrNull())
                        // okHttpRequestBody = emptyBody
                    }

                    when (request.method) {
                        "GET" -> okHttpRequestBuilder.get()
                        "POST" -> okHttpRequestBuilder.post(okHttpRequestBody ?: ByteArray(0).toRequestBody(null, 0, 0)) // Empty body if null
                        "PUT" -> okHttpRequestBuilder.put(okHttpRequestBody ?: ByteArray(0).toRequestBody(null, 0, 0))   // Empty body if null
                        "DELETE" -> okHttpRequestBuilder.delete(okHttpRequestBody)
                        "HEAD" -> okHttpRequestBuilder.head()
                        "PATCH" -> okHttpRequestBuilder.patch(okHttpRequestBody ?: ByteArray(0).toRequestBody(null, 0, 0)) // Empty body if null
                        else -> {
                            Log.e("WebViewDebug", "OkHttp - Unsupported method: ${request.method}")
                            okHttpRequestBuilder.method(request.method, okHttpRequestBody) // Generic method
                        }
                    }

                    val okhttpRequest = okHttpRequestBuilder.build()
                    val okhttpResponse: OkHttpResponse = okHttpClient.newCall(okhttpRequest).execute()

                    val statusCode = okhttpResponse.code
                    var reasonPhrase = okhttpResponse.message // Make it var
                    val responseBody = okhttpResponse.body

                    if (reasonPhrase.isEmpty()) {
                        reasonPhrase = if (statusCode in 200..299) "OK" else "Status $statusCode"
                        Log.d("WebViewDebug", "OkHttp - Empty reason phrase, defaulted to: $reasonPhrase for status $statusCode")
                    }

                    Log.d("WebViewDebug", "OkHttp - Response Code: $statusCode $reasonPhrase from $modifiedUrlString")

                    val responseHeadersOkHttp = okhttpResponse.headers
                    val responseHeadersMap = mutableMapOf<String, String>()
                    for (i in 0 until responseHeadersOkHttp.size) {
                        responseHeadersMap[responseHeadersOkHttp.name(i)] = responseHeadersOkHttp.value(i)
                    }

                    val contentType = responseBody?.contentType()?.toString()
                    val encoding = responseBody?.contentType()?.charset()?.name() ?: "UTF-8"

                    return WebResourceResponse(
                        contentType?.substringBefore(';'), // MIME type
                        encoding,
                        statusCode,
                        reasonPhrase,
                        responseHeadersMap,
                        responseBody?.byteStream() // This stream will be closed by WebView
                    )

                } catch (e: Exception) { // Catch generic Exception as OkHttp might throw various types
                    Log.e("CustomWebViewClient", "OkHttp - Error intercepting request to $requestUrl (IP ${sagraFacileServerIp}): ${e.message}", e)
                    val errorHtml = """
                        <html><body>
                        <h1>Connection Error (OkHttp)</h1>
                        <p>Failed to connect to the server at ${sagraFacileServerIp} for domain ${clientSagraFacileDomain}.</p>
                        <p>URL: ${requestUrl}</p>
                        <p>Error: ${e.javaClass.simpleName} - ${e.message}</p>
                        <p>Please check your network connection and the server IP/domain settings in the app.</p>
                        </body></html>
                    """.trimIndent()
                    return WebResourceResponse("text/html", "utf-8", 503, "Service Unavailable", mutableMapOf("Connection" to "close"), errorHtml.byteInputStream(Charsets.UTF_8))
                }
            }
            Log.d("WebViewDebug", "Not intercepting (OkHttp - passing to super): $requestUrl")
            return super.shouldInterceptRequest(view, request)
        }

        // Optional: Handle SSL errors if you were using self-signed certs (not recommended for production)
        // override fun onReceivedSslError(view: WebView?, handler: SslErrorHandler?, error: SslError?) {
        //     handler?.proceed() // DANGEROUS: Ignores SSL errors. Only for specific local dev scenarios.
        // }
    }

    // Custom SSLSocketFactory to enable SNI for a specific hostname when connecting via IP
    // This factory will be used by OkHttpClient
    private class SNISSLSocketFactory(private val sniHostname: String) : SSLSocketFactory() {
        private val customSslContext: SSLContext = SSLContext.getInstance("TLSv1.2").apply { // Or TLSv1.3 if preferred and widely supported
            init(null, null, null) // Initialize with default trust/key managers
        }
        private val underlyingSocketFactory: SSLSocketFactory = customSslContext.socketFactory

        override fun getDefaultCipherSuites(): Array<String> = underlyingSocketFactory.defaultCipherSuites
        override fun getSupportedCipherSuites(): Array<String> = underlyingSocketFactory.supportedCipherSuites

        @Throws(IOException::class)
        override fun createSocket(s: java.net.Socket?, host: String?, port: Int, autoClose: Boolean): java.net.Socket {
            Log.d("SNISSLSocketFactory", "createSocket(Socket, String, Int, Boolean) called. Target Host(IP): $host, Port: $port, ExistingSocket: $s, SNI Hostname for factory: $sniHostname")

            // If 's' is not null, an existing plain socket is being wrapped with SSL.
            // The 'host' parameter in createSocket(s, host, port, autoClose) is typically for identification/verification,
            // not for establishing a new connection since 's' is already connected.
            // Let's try passing our actual SNI hostname to the underlying factory when layering,
            // hoping it influences the SSL session setup more directly.
            val effectiveHostForUnderlyingFactory = if (s != null) sniHostname else host

            Log.d("SNISSLSocketFactory", "Calling underlyingSocketFactory.createSocket with s: $s, effectiveHost: $effectiveHostForUnderlyingFactory, port: $port, autoClose: $autoClose")
            val newUnderlyingSocket = underlyingSocketFactory.createSocket(s, effectiveHostForUnderlyingFactory, port, autoClose)

            if (newUnderlyingSocket is SSLSocket) {
                Log.i("SNISSLSocketFactory", "Configuring SSLSocket from createSocket(Socket, String, Int, Boolean). Class: ${newUnderlyingSocket.javaClass.name}")
                newUnderlyingSocket.useClientMode = true // CRITICAL: Ensure socket is in client mode
                Log.i("SNISSLSocketFactory", "Set useClientMode=true on socket: $newUnderlyingSocket")
                setSni(newUnderlyingSocket) // Apply SNI settings
            } else {
                Log.w("SNISSLSocketFactory", "Underlying factory did not return an SSLSocket in createSocket(Socket, String, Int, Boolean). Returned: ${newUnderlyingSocket?.javaClass?.name}")
            }
            return newUnderlyingSocket
        }

        @Throws(IOException::class)
        override fun createSocket(host: String?, port: Int): java.net.Socket {
            Log.d("SNISSLSocketFactory", "createSocket(String, Int) called. Host: $host, Port: $port")
            val newSocket = underlyingSocketFactory.createSocket(host, port)
            setSni(newSocket as? SSLSocket)
            return newSocket
        }

        @Throws(IOException::class)
        override fun createSocket(host: String?, port: Int, localHost: InetAddress?, localPort: Int): java.net.Socket {
            Log.d("SNISSLSocketFactory", "createSocket(String, Int, InetAddress, Int) called. Host: $host, Port: $port")
            val newSocket = underlyingSocketFactory.createSocket(host, port, localHost, localPort)
            setSni(newSocket as? SSLSocket)
            return newSocket
        }

        @Throws(IOException::class)
        override fun createSocket(host: InetAddress?, port: Int): java.net.Socket {
            Log.d("SNISSLSocketFactory", "createSocket(InetAddress, Int) called. Host: $host, Port: $port")
            val newSocket = underlyingSocketFactory.createSocket(host, port)
            setSni(newSocket as? SSLSocket)
            return newSocket
        }

        @Throws(IOException::class)
        override fun createSocket(address: InetAddress?, port: Int, localAddress: InetAddress?, localPort: Int): java.net.Socket {
            Log.d("SNISSLSocketFactory", "createSocket(InetAddress, Int, InetAddress, Int) called. Host: $address, Port: $port")
            val newSocket = underlyingSocketFactory.createSocket(address, port, localAddress, localPort)
            setSni(newSocket as? SSLSocket)
            return newSocket
        }

        @Throws(IOException::class)
        override fun createSocket(): java.net.Socket {
            Log.d("SNISSLSocketFactory", "createSocket() called.")
            val newSocket = underlyingSocketFactory.createSocket()
            setSni(newSocket as? SSLSocket)
            return newSocket
        }

        private fun setSni(socket: SSLSocket?) {
            Log.d("SNISSLSocketFactory", "setSni() called. Socket: $socket, Target SNI: $sniHostname")
            if (socket == null) {
                Log.w("SNISSLSocketFactory", "SSLSocket is null, cannot set SNI.")
                return
            }

            Log.i("SNISSLSocketFactory", "Socket class: ${socket.javaClass.name}, isConnected: ${socket.isConnected}, isBound: ${socket.isBound}, isClosed: ${socket.isClosed}")

            // Ensure useClientMode is true, as this is essential for client sockets.
            if (!socket.useClientMode) {
                Log.w("SNISSLSocketFactory", "Socket was not in client mode. Forcing useClientMode=true.")
                socket.useClientMode = true
            } else {
                Log.d("SNISSLSocketFactory", "Socket already in client mode.")
            }

            Log.d("SNISSLSocketFactory", "Supported Protocols: ${socket.supportedProtocols.joinToString()}")
            Log.d("SNISSLSocketFactory", "Enabled Protocols: ${socket.enabledProtocols.joinToString()}")
            Log.d("SNISSLSocketFactory", "Supported Cipher Suites: ${socket.supportedCipherSuites.joinToString()}")
            Log.d("SNISSLSocketFactory", "Enabled Cipher Suites: ${socket.enabledCipherSuites.joinToString()}")


            try {
                // Parameters are typically set before connect for client sockets.
                // HttpsURLConnection handles the connect call after the factory provides the socket.

                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.N) {
                    Log.i("SNISSLSocketFactory", "Using SSLParameters API (Android N+)")
                    val params = socket.sslParameters ?: SSLParameters() // Get existing or create new

                    Log.d("SNISSLSocketFactory", "Initial SSLParameters: ServerNames=${params.serverNames?.joinToString { (it as? javax.net.ssl.SNIHostName)?.asciiName ?: it.toString() }}, EndpointIDAlgo=${params.endpointIdentificationAlgorithm}")

                    val sniList = listOf(javax.net.ssl.SNIHostName(sniHostname))
                    params.serverNames = sniList
                    // params.endpointIdentificationAlgorithm = null // Consider if HostnameVerifier issues persist

                    socket.sslParameters = params // Apply the modified parameters

                    val newParams = socket.sslParameters
                    Log.i("SNISSLSocketFactory", "SNI configured with SSLParameters. Applied ServerNames: ${newParams.serverNames?.joinToString { (it as? javax.net.ssl.SNIHostName)?.asciiName ?: it.toString() }}")
                } else {
                    Log.i("SNISSLSocketFactory", "Using reflection (setHostname) for older Android (API < 24)")
                    try {
                        val setHostnameMethod = socket.javaClass.getMethod("setHostname", String::class.java)
                        setHostnameMethod.invoke(socket, sniHostname)
                        Log.i("SNISSLSocketFactory", "SNI configured with reflection (setHostname) to: $sniHostname")
                    } catch (e: NoSuchMethodException) {
                        Log.e("SNISSLSocketFactory", "Reflection: setHostname method not found on ${socket.javaClass.name}", e)
                        throw SSLException("SNI configuration failed: setHostname method not found", e)
                    } catch (e: Exception) {
                        Log.e("SNISSLSocketFactory", "Reflection: Error invoking setHostname on ${socket.javaClass.name}", e)
                        throw SSLException("SNI configuration failed via reflection", e)
                    }
                }
                Log.d("SNISSLSocketFactory", "setSni() finished successfully for $sniHostname.")
            } catch (e: Exception) {
                Log.e("SNISSLSocketFactory", "Exception in setSni for $sniHostname on socket $socket", e)
                if (e is SSLException) throw e
                throw SSLException("Failed to set SNI due to: ${e.message}", e)
            }
        }
    }
}

// Helper object for TrustManager to trust all certificates (USE WITH CAUTION - FOR DEVELOPMENT/SPECIFIC SCENARIOS ONLY)
// In a production app, you should use a proper TrustManager that validates the server certificate.
// This is included because OkHttp's .sslSocketFactory(factory, trustManager) requires an X509TrustManager.
object TrustManagerUtils {
    fun getTrustAllCerts(): Array<javax.net.ssl.TrustManager> {
        return arrayOf(object : javax.net.ssl.X509TrustManager {
            override fun checkClientTrusted(chain: Array<java.security.cert.X509Certificate>?, authType: String?) {
                Log.d("TrustManagerUtils", "checkClientTrusted: authType=$authType, chain non-null=${chain!=null}")
            }

            override fun checkServerTrusted(chain: Array<java.security.cert.X509Certificate>?, authType: String?) {
                Log.d("TrustManagerUtils", "checkServerTrusted: authType=$authType, chain non-null=${chain!=null}")
                // For debugging, log certificate details if needed
                // chain?.forEachIndexed { index, cert ->
                //     Log.d("TrustManagerUtils", "Server Cert $index: ${cert.subjectDN}")
                // }
            }

            override fun getAcceptedIssuers(): Array<java.security.cert.X509Certificate> {
                Log.d("TrustManagerUtils", "getAcceptedIssuers called")
                return arrayOf()
            }
        })
    }
}

package it.sagrafacile.androidapp

import android.content.Context
import android.os.Bundle
import android.widget.Button
import android.widget.EditText
import android.widget.Toast
import androidx.appcompat.app.AppCompatActivity

class SettingsActivity : AppCompatActivity() {

    private lateinit var editTextServerIp: EditText
    private lateinit var editTextServerDomain: EditText
    private lateinit var buttonSaveSettings: Button

    companion object {
        const val PREFS_NAME = "SagraFacilePrefs"
        const val KEY_SERVER_IP = "server_ip"
        const val KEY_SERVER_DOMAIN = "server_domain"
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_settings)

        editTextServerIp = findViewById(R.id.editTextServerIp)
        editTextServerDomain = findViewById(R.id.editTextServerDomain)
        buttonSaveSettings = findViewById(R.id.buttonSaveSettings)

        loadSettings()

        buttonSaveSettings.setOnClickListener {
            saveSettings()
        }
    }

    private fun loadSettings() {
        val prefs = getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
        val serverIp = prefs.getString(KEY_SERVER_IP, "")
        val serverDomain = prefs.getString(KEY_SERVER_DOMAIN, "")
        editTextServerIp.setText(serverIp)
        editTextServerDomain.setText(serverDomain)
    }

    private fun saveSettings() {
        val serverIp = editTextServerIp.text.toString().trim()
        val serverDomain = editTextServerDomain.text.toString().trim()

        if (serverIp.isNotEmpty() && serverDomain.isNotEmpty()) {
            val prefs = getSharedPreferences(PREFS_NAME, Context.MODE_PRIVATE)
            with(prefs.edit()) {
                putString(KEY_SERVER_IP, serverIp)
                putString(KEY_SERVER_DOMAIN, serverDomain)
                apply()
            }
            Toast.makeText(this, "Settings saved!", Toast.LENGTH_SHORT).show()
            finish() // Close the settings activity after saving
        } else {
            Toast.makeText(this, "Please enter a valid IP address and domain.", Toast.LENGTH_SHORT).show()
        }
    }
}

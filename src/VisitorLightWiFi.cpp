#include "VisitorLightWiFi.h"

#include <Preferences.h>
#include <WiFi.h>

namespace {

Preferences g_wifiPrefs;
String g_savedSsid;
String g_savedPassword;
bool g_haveCredentials = false;
bool g_wasConnected = false;
bool g_reconnectedEvent = false;
uint32_t g_lastReconnectAttemptMs = 0;
uint8_t g_reconnectAttempts = 0;

constexpr char kPrefsNamespace[] = "wifi";
constexpr char kSsidKey[] = "ssid";
constexpr char kPasswordKey[] = "pass";
constexpr uint32_t kReconnectIntervalMs = 5000;
constexpr uint8_t kFreshBeginEveryAttempts = 6;  // 30 seconds at 5-second retries

void startStationWithSavedCredentials() {
  if (!g_haveCredentials) {
    return;
  }

  // WiFi.begin() will enable the station interface. If a setup AP is already
  // active, the Arduino ESP32 core keeps the AP while enabling STA (AP+STA).
  WiFi.setAutoReconnect(true);
  WiFi.begin(g_savedSsid.c_str(), g_savedPassword.c_str());
}

}  // namespace

namespace VisitorLightWiFi {

bool loadCredentials() {
  g_wifiPrefs.begin(kPrefsNamespace, true);
  g_savedSsid = g_wifiPrefs.getString(kSsidKey, "");
  g_savedPassword = g_wifiPrefs.getString(kPasswordKey, "");
  g_wifiPrefs.end();

  g_haveCredentials = !g_savedSsid.isEmpty();
  return g_haveCredentials;
}

bool saveCredentials(const String &ssid, const String &password) {
  if (ssid.isEmpty()) {
    return false;
  }

  g_wifiPrefs.begin(kPrefsNamespace, false);
  const size_t ssidBytes = g_wifiPrefs.putString(kSsidKey, ssid);
  const size_t passBytes = g_wifiPrefs.putString(kPasswordKey, password);
  g_wifiPrefs.end();

  // An open network legitimately has a zero-length password, so the SSID write
  // is the required success condition. Refresh RAM from NVS after the write.
  (void)passBytes;
  loadCredentials();
  return ssidBytes > 0;
}

void clearCredentials() {
  g_wifiPrefs.begin(kPrefsNamespace, false);
  g_wifiPrefs.remove(kSsidKey);
  g_wifiPrefs.remove(kPasswordKey);
  g_wifiPrefs.end();

  g_savedSsid = "";
  g_savedPassword = "";
  g_haveCredentials = false;
  g_reconnectAttempts = 0;
  g_lastReconnectAttemptMs = 0;
}

bool beginSaved(uint32_t timeoutMs) {
  if (!loadCredentials()) {
    g_wasConnected = false;
    return false;
  }

  WiFi.setAutoReconnect(true);
  WiFi.mode(WIFI_STA);
  startStationWithSavedCredentials();

  const uint32_t startedMs = millis();
  while (WiFi.status() != WL_CONNECTED &&
         static_cast<uint32_t>(millis() - startedMs) < timeoutMs) {
    delay(100);
  }

  g_wasConnected = (WiFi.status() == WL_CONNECTED);
  g_reconnectedEvent = false;
  g_reconnectAttempts = 0;
  g_lastReconnectAttemptMs = millis();
  return g_wasConnected;
}

void service() {
  const bool connectedNow = (WiFi.status() == WL_CONNECTED);

  if (connectedNow) {
    if (!g_wasConnected) {
      g_reconnectedEvent = true;
    }
    g_wasConnected = true;
    g_reconnectAttempts = 0;
    return;
  }

  g_wasConnected = false;

  if (!g_haveCredentials && !loadCredentials()) {
    return;
  }

  const uint32_t nowMs = millis();
  if (static_cast<uint32_t>(nowMs - g_lastReconnectAttemptMs) <
      kReconnectIntervalMs) {
    return;
  }

  g_lastReconnectAttemptMs = nowMs;
  ++g_reconnectAttempts;
  WiFi.setAutoReconnect(true);

  // Normally let the ESP32 reconnect using its current station configuration.
  // Periodically issue a fresh begin with the known-good saved credentials so
  // recovery also works after a router reboot, mesh transition, or stale radio
  // state. Saved credentials are never erased because a connection attempt
  // failed.
  if (g_reconnectAttempts >= kFreshBeginEveryAttempts) {
    g_reconnectAttempts = 0;
    startStationWithSavedCredentials();
  } else {
    WiFi.reconnect();
  }
}

bool isConnected() {
  return WiFi.status() == WL_CONNECTED;
}

bool hasCredentials() {
  if (!g_haveCredentials) {
    loadCredentials();
  }
  return g_haveCredentials;
}

String savedSSID() {
  if (!g_haveCredentials) {
    loadCredentials();
  }
  return g_savedSsid;
}

bool consumeReconnected() {
  if (!g_reconnectedEvent) {
    return false;
  }

  g_reconnectedEvent = false;
  return true;
}

}  // namespace VisitorLightWiFi

#pragma once

#include <Arduino.h>

namespace VisitorLightWiFi {

// Load the saved station credentials from ESP32 nonvolatile Preferences.
// Namespace: "wifi"   Keys: "ssid", "pass"
bool loadCredentials();

// Save/replace the network used after reboot or power loss.
bool saveCredentials(const String &ssid, const String &password);

// Intentionally forget the saved network.
void clearCredentials();

// Connect to the saved network at boot. Returns true if connected before
// timeoutMs expires. A false return does NOT erase the saved network.
bool beginSaved(uint32_t timeoutMs = 15000);

// Call on every pass through loop(). This is deliberately non-blocking.
// Arduino auto-reconnect remains enabled, with an explicit reconnect retry
// every 5 seconds and a fresh WiFi.begin() every 30 seconds if necessary.
void service();

bool isConnected();
bool hasCredentials();
String savedSSID();

// Returns true once after a disconnected -> connected transition.
// Useful if the visitor-event poller should run immediately after recovery.
bool consumeReconnected();

}  // namespace VisitorLightWiFi

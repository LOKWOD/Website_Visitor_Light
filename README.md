# Website Visitor Light

ESP32-S3 website visitor indicator.

## Wi-Fi reconnect hardening

The repository now includes `src/VisitorLightWiFi.h` and `src/VisitorLightWiFi.cpp` for persistent Wi-Fi recovery.

Behavior:

- Wi-Fi SSID/password are stored in ESP32 `Preferences` namespace `wifi` using keys `ssid` and `pass`.
- On every power-up, firmware can load those credentials and reconnect automatically.
- ESP32 automatic reconnect is enabled.
- While disconnected, `VisitorLightWiFi::service()` retries without blocking the normal visitor-light loop.
- A reconnect is attempted every 5 seconds.
- Every 30 seconds of continued failure, the helper issues a fresh `WiFi.begin()` using the saved credentials.
- A temporary network failure never clears the saved credentials.
- `consumeReconnected()` allows the visitor poller to run immediately after the network returns.

### Integration into the existing visitor-light firmware

The working `src/main.cpp` still needs to be committed from the local PlatformIO project before this helper can be wired into it without risking changes to the existing RGB/Cloudflare visitor behavior.

Add the header:

```cpp
#include "VisitorLightWiFi.h"
```

At boot, use the saved network before falling back to setup mode:

```cpp
const bool wifiConnected = VisitorLightWiFi::beginSaved(15000);
if (!wifiConnected) {
  // Start the existing setup/captive-portal behavior here.
}
```

When the user submits Wi-Fi settings, save them with:

```cpp
VisitorLightWiFi::saveCredentials(ssid, password);
```

At the top of the normal `loop()`:

```cpp
VisitorLightWiFi::service();

if (VisitorLightWiFi::consumeReconnected()) {
  // Optional: force the visitor-event poller to run immediately.
}
```

Do not erase credentials merely because a connection attempt times out. Only call `VisitorLightWiFi::clearCredentials()` from an explicit "Forget/Reset Wi-Fi" action.

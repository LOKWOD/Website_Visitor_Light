# LOKWOD Website Visitor Light

ESP32-S3 website visitor indicator using the onboard WS2812 RGB LED on GPIO 48.

## Current firmware: v1.1.2

`firmware/src/main.cpp` is the current visitor-light firmware. v1.1.2 retains the v1.1.1 dashboard, Latest Visitor display, RGB visitor notifications, Cloudflare Worker polling, local dashboard authentication, Arduino OTA, and dashboard-only appraisal cash-register sound.

### Wi-Fi recovery behavior

- WiFiManager continues to save the selected Wi-Fi network in the ESP32's persistent Wi-Fi configuration.
- A normal power cycle reconnects to the saved network without requiring setup again.
- ESP32 automatic reconnect is enabled.
- If the connection drops while running, firmware explicitly calls `WiFi.reconnect()` every 5 seconds.
- If the unit remains offline for 90 seconds, it reboots into the saved-network recovery flow.
- The WiFiManager setup portal now times out after 120 seconds rather than remaining stuck indefinitely when the router/mesh is also rebooting after a power outage. The ESP32 then restarts and retries the saved network.
- Saved Wi-Fi is erased only through the existing **Reset Wi-Fi** action on the local dashboard.

This gives the light two recovery layers: normal ESP32 reconnect while running, followed by a full saved-network retry after a prolonged outage.

## Firmware project

The PlatformIO project is under `firmware/`.

```text
firmware/
  platformio.ini
  include/
    secrets.example.h
    trusted_roots.h
  src/
    main.cpp
```

Private `firmware/include/secrets.h` is intentionally excluded from Git. Copy `secrets.example.h` to `secrets.h` only when setting up a new local checkout, then insert the existing private values locally.

Build with:

```powershell
cd firmware
pio run -e esp32-s3-devkitc-1
```

Flash over USB with:

```powershell
pio run -e esp32-s3-devkitc-1 -t upload
```

A GitHub Actions workflow also performs a non-secret firmware compile check on repository updates.

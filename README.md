# LOKWOD Website Visitor Light

ESP32-S3 website visitor indicator using the onboard WS2812 RGB LED on GPIO 48.

## Current firmware: v1.1.3

v1.1.3 adds hands-off firmware updates while retaining the v1.1.2 Wi-Fi recovery, visitor RGB queue, Cloudflare Worker polling, Latest Visitor dashboard, local dashboard authentication, ArduinoOTA, and dashboard-only appraisal cash-register sound.

### Automatic firmware updates

- The light checks GitHub Releases about 45 seconds after boot, then every 6 hours.
- It only considers the latest published stable GitHub Release.
- Release tags use `vMAJOR.MINOR.PATCH` and are compared numerically; firmware will not intentionally downgrade itself.
- The required release asset is `LOKWOD_Visitor_Light.bin`.
- Downloads use certificate-validated HTTPS and the ESP32 OTA partition.
- A failed update leaves the currently running firmware in place and retries the release check later.
- The dashboard shows the installed version, latest release, update status, and includes a **Check for firmware update** button.
- The repository workflow builds `LOKWOD_Visitor_Light.bin` and automatically publishes a GitHub Release whenever `kFirmwareVersion` is bumped to a version that does not already have a release.

**Important:** v1.1.3 must be installed once over USB (or the existing local Arduino OTA path). After that, future published versions can install themselves.

### Persistent private device configuration

Public GitHub release binaries must not contain private device credentials. v1.1.3 migrates the existing Worker URL, device token, dashboard password, OTA password, and setup password into ESP32 nonvolatile storage on the first boot. Future public release binaries preserve and reuse those stored values instead of replacing them with CI placeholder values.

## Wi-Fi recovery behavior

- WiFiManager saves the selected Wi-Fi network in the ESP32's persistent Wi-Fi configuration.
- A normal power cycle reconnects to the saved network without setup.
- ESP32 automatic reconnect remains enabled.
- A dropped connection is explicitly retried every 5 seconds.
- After 90 seconds offline, the unit restarts into saved-network recovery.
- The setup portal times out after 120 seconds and retries rather than becoming permanently stranded during a router/mesh outage.
- Saved Wi-Fi is erased only through **Reset Wi-Fi** on the local dashboard.

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

Private `firmware/include/secrets.h` is intentionally excluded from Git. It is used for the one-time private v1.1.3 migration build; CI builds use `secrets.example.h`, because device-specific credentials are preserved in ESP32 nonvolatile storage after migration.

Build locally with:

```powershell
cd firmware
pio run -e esp32-s3-devkitc-1
```

Flash over USB with:

```powershell
pio run -e esp32-s3-devkitc-1 -t upload
```

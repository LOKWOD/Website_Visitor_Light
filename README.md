# LOKWOD Website Visitor Light

ESP32-S3 website visitor indicator using the onboard WS2812 RGB LED on GPIO 48.

## Current firmware: v1.1.8

v1.1.8 adds device heartbeat reporting without changing the visitor-light event path.

### Device heartbeat

Once per minute the ESP32 sends an authenticated `POST /v1/heartbeat` to its configured Cloudflare Worker. The payload contains only device-health information:

- firmware version
- device uptime
- Wi-Fi RSSI
- age of the last successful visitor-event poll
- today's accepted visitor count
- free ESP32 heap

The heartbeat deliberately does **not** send the Wi-Fi SSID, Wi-Fi password, dashboard password, OTA password, device token, public visitor IPs, or the ESP32 local IP.

Heartbeat failure is non-fatal. If the Worker heartbeat route is unavailable, the ESP32 continues normal `/v1/events` polling, RGB visitor flashes, dashboard service, Wi-Fi recovery, and OTA update checks.

The local dashboard now shows heartbeat acknowledgement state and the age of the last successful acknowledgement.

### Cloudflare heartbeat persistence

`cloudflare/heartbeat-extension.js` contains the server-side persistence helpers intended for the existing production `lokwod-analytics` Worker.

The production Worker must keep its existing device Bearer-token authorization on `POST /v1/heartbeat`. The helper stores only sanitized device-health data in a Workers KV binding named `DEVICE_STATUS` and exposes a sanitized status response through `GET /v1/heartbeat/status` once integrated.

**Do not replace the production Worker wholesale with the helper file.** It is an extension to the existing visitor-tracking and firmware-proxy Worker.

## Visitor polling

- The light polls the configured Cloudflare Worker every 3 seconds.
- New visitor events are queued and flash the onboard RGB LED using each site's assigned color.
- The dashboard shows accepted visits, Worker connectivity, the latest visitor, approximate visitor area, event queue depth, uptime, firmware status, and heartbeat state.
- Full visitor IP addresses are not displayed on the dashboard.

## Automatic firmware updates

- The light checks for firmware updates after boot and then every 6 hours.
- Current releases route firmware manifest checks and downloads through the authenticated Cloudflare Worker.
- Release tags use `vMAJOR.MINOR.PATCH` and are compared numerically; the firmware does not intentionally downgrade itself.
- The release asset is `LOKWOD_Visitor_Light.bin`.
- A failed update leaves the currently running firmware in place and retries later.
- The dashboard includes a **Check for firmware update** button.

## Saveable passwords and dashboard login

The v1.1.4+ firmware retains saveable dashboard, setup-AP, and Arduino OTA passwords in ESP32 nonvolatile `Preferences` storage. Device-specific credentials survive power cycles and firmware updates and are not embedded in public GitHub release binaries.

The dashboard username remains `admin`.

## Wi-Fi recovery

- Saved Wi-Fi reconnects automatically after normal power cycles.
- A dropped connection is retried every 5 seconds.
- After 90 seconds offline, the unit restarts into saved-network recovery.
- The setup portal times out rather than remaining stranded indefinitely.
- Saved Wi-Fi is erased only through **Reset Wi-Fi** on the local dashboard.

## Firmware project

The PlatformIO project is under `firmware/`.

```text
firmware/
  platformio.ini
  include/
    secrets.example.h
    trusted_roots.h
  patches/
    v1.1.4-saveable-passwords.patch
  src/
    main.cpp
```

Private `firmware/include/secrets.h` is intentionally excluded from Git. CI builds use `secrets.example.h`; device-specific credentials are preserved in ESP32 nonvolatile storage.

Build locally with:

```powershell
cd firmware
pio run -e esp32-s3-devkitc-1
```

Flash over USB with:

```powershell
pio run -e esp32-s3-devkitc-1 -t upload
```

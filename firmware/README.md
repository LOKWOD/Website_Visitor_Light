# ESP32-S3 Website Visitor Light Firmware

Target board: QIQIAZI / ESP32-S3-DevKitC-1 N16R8 with the onboard WS2812 RGB LED on GPIO 48.

## Current release: v1.1.12

v1.1.12 closes each local dashboard HTTP connection after sending its response. This prevents a browser keep-alive connection from occupying the ESP32 web server and delaying or blocking the Windows Visitor Key companion's login and status requests.

v1.1.11 adds a private Last 50 Visitors table with timestamp, full visitor IP, source/referrer, destination site/page/title, and approximate location. The ESP32 proxies the authenticated Worker history feed so the device token is never sent to the dashboard browser.

Existing devices update automatically through the connected Cloudflare Worker. For a fresh device, install the current firmware once using USB or local Arduino OTA. Private Worker/device and local-access credentials are stored in ESP32 Preferences so later public GitHub release binaries can update the unit without embedding those secrets.

After migration, the light checks the latest published GitHub Release about 45 seconds after boot and every 6 hours. If the release contains a higher `vMAJOR.MINOR.PATCH` tag plus `LOKWOD_Visitor_Light.bin`, it downloads and installs the firmware over certificate-validated HTTPS and reboots automatically.

The local dashboard shows:

- current firmware version
- latest GitHub release discovered
- automatic update status
- **Check for firmware update** button for an immediate test
- Last 50 visitor events with IP, source, destination, and approximate area

## First USB upload / fresh device

1. Open this `firmware` folder in Visual Studio Code.
2. Let PlatformIO install the declared libraries.
3. Create `include/secrets.h` from the private configuration or `secrets.example.h`.
4. Build with the checkmark.
5. Upload with the right-arrow button using the USB-to-UART connector.
6. Open Serial Monitor at 115200 baud.

On a fresh device, the board creates `LOKWOD-Visitor-Light`. Existing installations keep their saved Wi-Fi and local credentials.

## Normal behavior

- LED off at idle.
- Accepted visitors pulse three times in the website color.
- Worker poll every three seconds through certificate-validated HTTPS.
- First poll bootstraps at the current cursor so historical visits do not flash.
- Event cursor persists through restarts.
- Wi-Fi reconnect retry every five seconds with a restart fallback after a prolonged outage.
- Local RGB test, end-to-end cloud test, Latest Event card, visitor history, and affiliate-click cash-register sound remain available.
- ArduinoOTA remains available as a local maintenance fallback.
- GitHub Release OTA through the Cloudflare Worker is the normal hands-off update path.

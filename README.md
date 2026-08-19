# LOKWOD Website Visitor Light

ESP32-S3 website visitor indicator using the onboard WS2812 RGB LED on GPIO 48.

## Current stable firmware: v1.1.4

v1.1.4 keeps the v1.1.3 hands-off GitHub firmware updater and adds saveable local passwords plus a normal rememberable dashboard login.

### Saveable passwords and dashboard login

- The dashboard now has a **Security settings** card.
- Dashboard, setup-AP, and Arduino OTA passwords can be changed from the local dashboard.
- Passwords are stored in ESP32 nonvolatile `Preferences` and survive power cycles, restarts, and future firmware updates.
- Leave a password field blank to keep its current value.
- Requested password changes are validated before any are written; passwords must be 8 to 63 characters.
- Dashboard username remains `admin`.
- The old browser Basic-Auth popup is replaced by a normal HTML login form using standard password-manager/autofill fields.
- **Remember me on this device** creates a persistent browser session for up to 30 days.
- The remembered server-side session token is also stored on the ESP32, so the login can survive ESP32 restarts and firmware updates.
- Changing the dashboard password rotates the session token and invalidates previously remembered browser sessions.
- A **Sign out** button is available under Maintenance.

### Automatic firmware updates

- The light checks GitHub Releases about 45 seconds after boot, then every 6 hours.
- It only considers the latest published stable GitHub Release.
- Release tags use `vMAJOR.MINOR.PATCH` and are compared numerically; firmware will not intentionally downgrade itself.
- The required release asset is `LOKWOD_Visitor_Light.bin`.
- Downloads use certificate-validated HTTPS and the ESP32 OTA partition.
- A failed update leaves the currently running firmware in place and retries the release check later.
- The dashboard shows the installed version, latest release, update status, and includes a **Check for firmware update** button.
- The repository workflow builds `LOKWOD_Visitor_Light.bin` and automatically publishes a GitHub Release for a new stable firmware version.

**Important:** a device on v1.1.3 can receive v1.1.4 automatically. A device still on v1.1.2 or earlier should use the v1.1.4 USB bridge installer once; normal later releases can then install themselves.

### Persistent private device configuration

Public GitHub release binaries do not contain private device credentials. The v1.1.3+ firmware migrates/preserves the Worker URL, device token, dashboard password, OTA password, and setup password in ESP32 nonvolatile storage. Public CI-built releases reuse those stored values instead of replacing them with placeholder values.

## Wi-Fi recovery behavior

- WiFiManager saves the selected Wi-Fi network in the ESP32's persistent Wi-Fi configuration.
- A normal power cycle reconnects to the saved network without setup.
- ESP32 automatic reconnect remains enabled.
- A dropped connection is explicitly retried every 5 seconds.
- After 90 seconds offline, the unit restarts into saved-network recovery.
- The setup portal times out after 120 seconds and retries rather than becoming permanently stranded during a router/mesh outage.
- Saved Wi-Fi is erased only through **Reset Wi-Fi** on the local dashboard.

## Firmware project

The PlatformIO project is under `firmware/`. The repository currently retains the v1.1.3 baseline source plus the compatibility patch used to build the v1.1.4 stable release.

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

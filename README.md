# LOKWOD Website Visitor Light

ESP32-S3 website visitor indicator using the onboard WS2812 RGB LED on GPIO 48.

## Current firmware: v1.1.12

v1.1.12 prevents the local dashboard's persistent browser connection from monopolizing the ESP32 web server. Every authenticated dashboard, login, status, and history response now closes its HTTP connection after delivery so the browser and Windows Visitor Key companion can poll reliably at the same time.

v1.1.11 adds a private **Last 50 visitors** table to the authenticated local dashboard. Each new event records its timestamp, full visitor IP address, approximate city/region/country, referrer/source, destination website, page path, page title or affiliate link, visitor ID, and event type. The browser never receives the private Worker device token; the ESP32 uses it to proxy the history from the Worker into the password-protected dashboard.

v1.1.10 added private affiliate-click notifications. An active affiliate link click produces a gold six-pulse LED event. The optional cash-register sound is generated only by the owner's authenticated local Visitor Light dashboard after the owner enables it; public website visitors never hear it.

The ESP32-S3 board itself has an RGB LED but no speaker. Audible notifications therefore play through the phone or computer displaying the private local dashboard. Adding sound directly to the physical device requires buzzer or speaker hardware.

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
- The authenticated dashboard shows the 50 newest visitor/affiliate events with full IP, approximate area, referrer/source, and destination page.
- Raw visitor IP addresses and referrers are available only through the device-token-protected `/v1/history` Worker route and the password-protected ESP32 dashboard.
- History collection starts when the v1.3.0 Worker is deployed; older retained events can show `Not recorded` for fields the previous Worker did not save.

## Tracked site colors

Every tracked website has its own visibly separated RGB color. The Worker is the canonical color source sent with each visitor event.

| Website | Site ID | LED color | RGB |
|---|---|---:|---:|
| NauticalDream.com | `nautical-dream` | Aqua | `0, 185, 255` |
| LOKWOD.com | `lokwod` | Blue | `0, 82, 255` |
| LifeInTheSimulation.com | `life-in-the-simulation` | Green | `0, 235, 95` |
| BeautifulMensClub.com | `beautiful-mens-club` | Purple | `180, 35, 255` |
| MrAdventureDad.com | `mr-adventure-dad` | Orange | `255, 96, 0` |
| SyracuseAppraiser.com | `syracuse-appraiser` | Amber | `255, 174, 0` |
| AccurateREAppraisals.com | `accurate-re-appraisals` | White | `235, 235, 235` |
| AccurateREAppraisals.org | `accurate-re-appraisals-org` | Red | `255, 0, 0` |
| DishGal.com | `dish-gal` | Hot pink | `255, 0, 140` |
| Blappos.com | `blappos` | Teal | `0, 255, 180` |
| BigBudMan.com | `big-bud-man` | Lime | `170, 255, 0` |
| TheCryptoAppraiser.com | `the-crypto-appraiser` | Indigo | `75, 0, 255` |

Run `node cloudflare/validate-site-colors.mjs` after changing the map. The validator rejects missing sites, duplicate RGB values, and colors that are too visually close.

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


## Dark Mount Visitor Key (Windows)

The Windows companion under `windows/visitor-key/` turns the upper-right be quiet! Dark Mount Display Key into a second Visitor Light. It shows the newest visitor or affiliate-click color, opens the private local dashboard when pressed, encrypts the dashboard password with Windows DPAPI, and backs up the original key image before its first write. It observes the existing authenticated ESP32 dashboard and does not consume or alter the ESP32 event queue.

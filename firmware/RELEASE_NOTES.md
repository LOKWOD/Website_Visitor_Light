# Visitor Light Firmware Releases

## v1.1.12

- Closes local dashboard HTTP connections after each response so one browser cannot monopolize the ESP32 web server.
- Allows the browser dashboard and Windows Visitor Key companion to poll the ESP32 reliably at the same time.
- Keeps visitor flashing, history, affiliate-click alerts, heartbeat, saved credentials, and Cloudflare-proxied OTA behavior unchanged.

## v1.1.11

- Adds a private Last 50 Visitors dashboard table.
- Shows full visitor IP, timestamp, referrer/source, destination site/page/title, visitor ID, event type, and approximate location.
- Retrieves history through the ESP32 so the Worker device token is never exposed to the dashboard browser.
- Keeps visitor flashing, affiliate-click money alerts, heartbeat, password storage, Wi-Fi recovery, and Cloudflare-proxied OTA behavior unchanged.

## v1.1.5

- Adds **Dish Gal** to the dashboard **Site colors** list with an intentionally unassigned/blank swatch.
- Includes the corrected GitHub HTTPS trust chain required by the automatic updater.
- No visitor-event, Wi-Fi recovery, password-storage, dashboard-login, Cloudflare polling, RGB queue, or cash-register behavior is intentionally changed.

This release is being used as the first end-to-end validation of the Visitor Light's automatic GitHub firmware update process.

# LOKWOD Visitor Key

Windows companion for the be quiet! Dark Mount and the LOKWOD Website Visitor Light.

## v1.0.2 visitor-only alerts

- Startup, connection, reconnection, and watchdog activity is completely silent.
- The corner card and Windows notification sound occur only for a real visitor, affiliate click, or a manual **Test visitor key**.
- The visitor image now uses display key 4, directly above the previous key 8 position.
- During installation, the original key 8 image is restored and key 4 is backed up before use.

## v1.0.1 reliability update

- Automatically signs back in when the ESP32 dashboard session expires instead of silently stopping.
- Uses a dedicated always-on-top corner notification that Windows cannot throttle like repeated tray balloons.
- Keeps the corner notification working even when the keyboard is temporarily disconnected or busy.
- Reconnects the Dark Mount key listener after USB resets, sleep, or wake.
- Serializes Display Key commands so the key listener cannot consume an image-transfer response.
- Restarts a stalled network poller and refreshes its HTTP connection after repeated failures.
- Prevents two copies of the companion from competing for the same keyboard.
- Writes a small rotating diagnostic log available from the tray menu.

- Uses the upper-right Display Key (hardware key 8 / `0x74`).
- Shows the color and name of the newest website visitor or affiliate click.
- Pulses the keyboard lighting three times in the same site color when the optional LampArray interface is available. On this Windows 10 PC, the screen button itself remains the visitor indicator.
- Opens the private Visitor Light dashboard when the key is pressed.
- Polls the already-authenticated ESP32 dashboard; it does not consume or alter the ESP32 event queue.
- Encrypts the dashboard password with Windows DPAPI for the current Windows account.
- Backs up the original key image before the first write and can restore it from the tray menu.

The app allows only QLink session commands and Display Key image read/write commands. It does not implement firmware, bootloader, factory-reset, or configuration commands.

## Install

Download and extract `LOKWOD-Visitor-Key-v1.0.2-win-x64.zip`, then double-click `INSTALL.cmd`. The installer preserves the saved dashboard password and original key images, updates the Startup shortcut, and launches the repaired companion.

## Build

Requires the .NET 8 SDK. A local Windows publish copies `hidapi.dll` from be quiet! IO Center when available; the packaged installer can also copy it from the installed IO Center at install time.

```powershell
dotnet publish -c Release -r win-x64 --self-contained true
```

Run `Install-Visitor-Key.ps1` after publishing. It installs to the current user's Local AppData folder, preserves the first backup of key 8, and creates a current-user Startup shortcut.

## Protocol acknowledgement

The Dark Mount USB identifiers, Display Key IDs, image framing, QLink session framing, CRC, and HID LampArray details were independently implemented from the public protocol notes in [re133/iocenter-linux](https://github.com/re133/iocenter-linux). No source code from that project is included here.

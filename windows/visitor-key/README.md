# LOKWOD Visitor Key

Windows companion for the be quiet! Dark Mount and the LOKWOD Website Visitor Light.

- Uses the upper-right Display Key (hardware key 8 / `0x74`).
- Shows the color and name of the newest website visitor or affiliate click.
- Pulses the keyboard lighting three times in the same site color when the optional LampArray interface is available. On this Windows 10 PC, the screen button itself remains the visitor indicator.
- Opens the private Visitor Light dashboard when the key is pressed.
- Polls the already-authenticated ESP32 dashboard; it does not consume or alter the ESP32 event queue.
- Encrypts the dashboard password with Windows DPAPI for the current Windows account.
- Backs up the original key image before the first write and can restore it from the tray menu.

The app allows only QLink session commands and Display Key image read/write commands. It does not implement firmware, bootloader, factory-reset, or configuration commands.

## Build

Requires the installed be quiet! IO Center `hidapi.dll` and the .NET 5 SDK.

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

Run `Install-Visitor-Key.ps1` after publishing. It installs to the current user's Local AppData folder, preserves the first backup of key 8, and creates a current-user Startup shortcut.

## Protocol acknowledgement

The Dark Mount USB identifiers, Display Key IDs, image framing, QLink session framing, CRC, and HID LampArray details were independently implemented from the public protocol notes in [re133/iocenter-linux](https://github.com/re133/iocenter-linux). No source code from that project is included here.

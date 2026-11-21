# Fake Nintendo Connection Check (Local DNS & HTTP Server)

A lightweight tool that simulates Nintendo's internet connectivity check (`ctest` / `conntest`) locally while blocking all other external domains (`NXDOMAIN`).

This keeps a Nintendo Switch or Nintendo Switch 2 connected to an offline/air-gapped local network (e.g., for local payloads, FTP, or HTTP servers) without dropping the Wi-Fi connection and without ever reaching Nintendo or third-party servers.

## Versions

| Version | Language | Platform | Description |
|---------|----------|----------|-------------|
| **js1.x.x** | Node.js | Windows, Linux, macOS | Console-based, zero dependencies |
| **c#1.x.x** | C# (.NET 8) | Windows | WPF GUI with visual connection indicator |

Choose the version that best fits your needs. The C# version requires Windows and provides a graphical interface, while the JavaScript version works cross-platform.

---

## JavaScript Version (js1.x.x)

### Features

- **Zero External Dependencies:** Built exclusively with native Node.js modules (`dgram`, `http`, `os`, `readline`).
- **Interactive Interface Selection:** Automatically lists all available IPv4 network adapters on your machine so you can bind specifically to your local LAN/Wi-Fi interface (avoiding VPN or virtual adapters).
- **Strict Domain Filtering:**
  - **Allowed:** Only connectivity test domains (`ctest.p01.ctest.srv.nintendo.net`, `ctest.cdn.nintendo.net`, `conntest.nintendowifi.net`) are resolved to your local PC's IP address and answered with the expected `X-Organization: Nintendo` HTTP header.
  - **Blocked:** All other DNS queries (system updates, telemetry, authentication, game servers) immediately return `NXDOMAIN`.
- **100% Offline Capable:** Works in completely air-gapped routers/hotspots without an internet connection.

### Prerequisites

- [Node.js](https://nodejs.org/) (v16 or newer)
- Administrator / `sudo` privileges (required to bind to UDP port `53` and TCP port `80`)

### Usage

#### 1. Start the Server on Your PC
Open PowerShell **as Administrator** (Windows) or with `sudo` (Linux/macOS) and run:

```bash
cd <folder/of/your/fscc.js>
node fscc.js
```

Select the number corresponding to the local network adapter connected to your offline router/network.

#### 2. Configure Your Console
1. Go to **System Settings** -> **Internet** -> **Internet Settings**.
2. Select your local Wi-Fi network -> **Change Settings**.
3. Set **DNS Settings** to **Manual**.
4. Enter the IP address displayed by `fscc.js` for both **Primary DNS** and **Secondary DNS**.
5. Save and select **Connect to this Network**.
<img width="1367" height="691" alt="image" src="https://github.com/user-attachments/assets/451472b8-f42b-4f33-afb8-0b3a6b14f68d" />


---

## C# Version (c#1.x.x)

### Features

- **Graphical User Interface:** WPF-based interface with network adapter selection, start/stop controls, and real-time logging.
- **Visual Connection Indicator:** Subtle pulsing ring shows active connection status (green = connected, gray = waiting).
- **Strict Domain Filtering:** Same DNS/HTTP emulation as the JavaScript version.
- **Keepalive Check:** Automatically detects when the Switch stops sending requests and resets the indicator after 10 seconds of inactivity.
- **Clean Shutdown:** DNS and HTTP servers are fully stopped when clicking Stop, releasing ports 53 and 80.
- **100% Offline Capable:** Works in completely air-gapped routers/hotspots without an internet connection.

### Known Issues

- The connection indicator may briefly switch back to **"No connection"** even while the Switch is still connected. This is caused by the keepalive timeout being shorter than the Switch's actual request interval in some scenarios. A fix is planned for a future release.

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or runtime
- Windows 10 or newer
- Administrator privileges (required to bind to UDP port `53` and TCP port `80`)

### Usage

#### 1. Start the Server on Your PC
Open PowerShell **as Administrator** and run:

```powershell
cd <folder/of/fscc-csharp>
dotnet run
```

Alternatively, build a standalone executable:

```powershell
dotnet publish -c Release -o dist
dist\fscc.exe
```

#### 2. Using the GUI
1. **Select** your network adapter from the dropdown (shows adapter name + IP).
2. **Click** `▶ Start` to begin DNS/HTTP emulation.
3. The connection ring turns **green** and pulses gently when the server is active.
4. The **Log window** shows all DNS queries (`[ALLOWED]` / `[BLOCKED]`) and HTTP requests in real-time.
5. **Click** `⏹ Stop` to shut down the server and release ports 53/80.

#### 3. Configure Your Console
Same as the JavaScript version — set your console's DNS to the IP shown in the application.
<img width="916" height="736" alt="image" src="https://github.com/user-attachments/assets/0f5e998d-d653-4d9d-afa5-c3a9e7f997d8" />


---

## Troubleshooting

- **`EADDRINUSE` on Port 53 or Port 80:**
  - On Windows, stop the Internet Connection Sharing service if you previously used Mobile Hotspot: `net stop SharedAccess`
  - If Windows HTTP.sys occupies port 80, run: `net stop http`
  - On Linux, `systemd-resolved` may occupy port 53; bind specifically to your LAN IP (already handled by this script) or temporarily stop `systemd-resolved`.
- **Console Times Out on Connection Check:**
  - Ensure your Windows Firewall allows inbound UDP port `53` and TCP port `80` (or `dotnet.exe` / `fscc.exe`) on your local network profile, and verify that your network profile is set to **Private** rather than **Public**.

## License

MIT

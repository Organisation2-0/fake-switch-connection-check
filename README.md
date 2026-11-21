# Fake Nintendo Connection Check (Local DNS & HTTP Server)

A lightweight, zero-dependency Node.js tool that simulates Nintendo's internet connectivity check (`ctest` / `conntest`) locally while blocking all other external domains (`NXDOMAIN`). 

This keeps a Nintendo Switch or Nintendo Switch 2 connected to an offline/air-gapped local network (e.g., for local payloads, FTP, or HTTP servers) without dropping the Wi-Fi connection and without ever reaching Nintendo or third-party servers.

## Features

- **Zero External Dependencies:** Built exclusively with native Node.js modules (`dgram`, `http`, `os`, `readline`).
- **Interactive Interface Selection:** Automatically lists all available IPv4 network adapters on your machine so you can bind specifically to your local LAN/Wi-Fi interface (avoiding VPN or virtual adapters).
- **Strict Domain Filtering:**
  - **Allowed:** Only connectivity test domains (`ctest.p01.ctest.srv.nintendo.net`, `ctest.cdn.nintendo.net`, `conntest.nintendowifi.net`) are resolved to your local PC's IP address and answered with the expected `X-Organization: Nintendo` HTTP header.
  - **Blocked:** All other DNS queries (system updates, telemetry, authentication, game servers) immediately return `NXDOMAIN`.
- **100% Offline Capable:** Works in completely air-gapped routers/hotspots without an internet connection.

## Prerequisites

- [Node.js](https://nodejs.org/) (v16 or newer)
- Administrator / `sudo` privileges (required to bind to UDP port `53` and TCP port `80`)


## Usage

### 1. Start the Server on Your PC
Open PowerShell **as Administrator** (Windows) or with `sudo` (Linux/macOS) and run:

```bash
cd <folder/of/your/fscc.js>
```
and then

```bash
node fscc.js
```

Select the number corresponding to the local network adapter connected to your offline router/network.

### 2. Configure Your Console
1. Go to **System Settings** -> **Internet** -> **Internet Settings**.
2. Select your local Wi-Fi network -> **Change Settings**.
3. Set **DNS Settings** to **Manual**.
4. Enter the IP address displayed by `fscc.js` for both **Primary DNS** and **Secondary DNS**.
5. Save and select **Connect to this Network**.

## Troubleshooting

- **`EADDRINUSE` on Port 53 or Port 80:**
  - On Windows, stop the Internet Connection Sharing service if you previously used Mobile Hotspot: `net stop SharedAccess`
  - If Windows HTTP.sys occupies port 80, run: `net stop http`
  - On Linux, `systemd-resolved` may occupy port 53; bind specifically to your LAN IP (already handled by this script) or temporarily stop `systemd-resolved`.
- **Console Times Out on Connection Check:**
  - Ensure your Windows Firewall allows inbound UDP port `53` and TCP port `80` (or `node.exe`) on your local network profile, and verify that your network profile is set to **Private** rather than **Public**.

## License

MIT

# fscc - Nintendo Connection Check Server Emulator

Emuliert die Nintendo Connection-Check-Server (DNS + HTTP), damit eine
Nintendo Switch (1 oder 2) auch ohne Internet eine Verbindung annimmt.

## Funktion

- **DNS (UDP Port 53)**: Domänen, die `ctest` oder `conntest` enthalten,
  werden auf die lokale IP aufgelöst. Alle anderen Domänen erhalten NXDOMAIN.
- **HTTP (Port 80)**: Gibt immer `200 OK` mit Body `ok` zurück.
- **Keepalive**: Wenn 10 Sekunden keine Switch-Anfrage mehr eintrifft,
  wechselt die Anzeige zurück auf „No connection".

## Voraussetzungen

- Windows
- Administratorrechte (für Port 53 und 80)

## Starten

### Release (empfohlen)

Lade die `fscc.exe` aus dem [Latest Release](../../releases/latest) herunter
und starte sie direkt. Keine weitere Installation nötig.

### Aus dem Quellcode

```
dotnet run
```

Erfordert .NET 8.0 SDK.

## Bedienung

Eine grafische Oberfläche öffnet sich:

1. Netzwerkadapter auswählen (Name + IP wird angezeigt)
2. Auf **▶ Start** klicken
3. Verbindungsanzeige zeigt pulsierenden Ring, wenn die Switch pingt
4. Log-Fenster zeigt DNS/HTTP-Aktivitäten

## Projektstruktur

```
fscc-csharp/
├── fscc.csproj      # WPF-Projekt (.NET 8)
├── AppIcon.ico      # Anwendungssymbol
├── App.xaml         # WPF-Anwendungseinstieg
├── App.xaml.cs
├── MainWindow.xaml  # Grafische Oberfläche
├── MainWindow.xaml.cs
├── FscsServer.cs    # Server-Logik (DNS + HTTP)
└── README.md
```

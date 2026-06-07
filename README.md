# ILS Eggingen Silent Print

Lokale Windows-Desktop-Anwendung, die als HTTP-Server läuft und Druckaufträge von der ILS Eggingen Webapp empfängt und diese ohne Benutzerinteraktion (silent) auf einem vorkonfigurierten Drucker ausgibt.

## Voraussetzungen

- Windows 10/11
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (zum Entwickeln)
- [WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) (siehe Installation unten)

### WebView2 Runtime installieren

Die App benötigt die **Microsoft Edge WebView2 Runtime**. Diese ist auf den meisten Windows 10/11 Systemen bereits vorinstalliert (kommt mit Microsoft Edge).

**Prüfen ob installiert:**

```powershell
# Version in der Registry prüfen
Get-ItemProperty "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" -ErrorAction SilentlyContinue | Select-Object pv
```

**Falls nicht installiert – Installation via CLI (ohne Admin-Rechte):**

```powershell
# Evergreen Bootstrapper herunterladen und per-user installieren
Invoke-WebRequest -Uri "https://go.microsoft.com/fwlink/p/?LinkId=2124703" -OutFile "$env:TEMP\MicrosoftEdgeWebview2Setup.exe"
Start-Process -FilePath "$env:TEMP\MicrosoftEdgeWebview2Setup.exe" -ArgumentList "/silent /install" -Wait
```

**Oder als Admin (system-weit):**

```powershell
# Als Administrator in PowerShell:
Invoke-WebRequest -Uri "https://go.microsoft.com/fwlink/p/?LinkId=2124703" -OutFile "$env:TEMP\MicrosoftEdgeWebview2Setup.exe"
Start-Process -FilePath "$env:TEMP\MicrosoftEdgeWebview2Setup.exe" -ArgumentList "/silent /install" -Wait -Verb RunAs
```

**Alternativ:** [Manueller Download](https://developer.microsoft.com/en-us/microsoft-edge/webview2/) → "Evergreen Bootstrapper" herunterladen und ausführen.

## Schnellstart

```bash
# Projekt bauen
dotnet build

# Projekt starten
dotnet run
```

## Veröffentlichen

```bash
dotnet publish -c Release
```

Die fertige `.exe` befindet sich unter `bin\Release\net9.0-windows\win-x64\publish\`.

## Funktionsweise

Die App startet als System-Tray-Anwendung und stellt einen lokalen HTTP-Server auf Port 9150 bereit.

### API-Endpunkte

| Methode | Pfad      | Beschreibung          |
| ------- | --------- | --------------------- |
| GET     | `/health` | Health-Check          |
| POST    | `/print`  | Druckauftrag senden   |
| GET     | `/config` | Konfiguration abrufen |

### Druckauftrag senden

**PowerShell:**

```powershell
Invoke-RestMethod -Uri "http://localhost:9150/print" -Method Post -ContentType "application/json" -Body '{"html": "<h1>Test</h1>", "copies": 1}'
```

**PowerShell (Health-Check):**

```powershell
Invoke-RestMethod -Uri "http://localhost:9150/health"
```

**curl (Git Bash / CMD):**

```bash
curl -X POST http://localhost:9150/print -H "Content-Type: application/json" -d "{\"html\": \"<h1>Test</h1>\", \"copies\": 1}"
```

> **Hinweis:** In PowerShell ist `curl` ein Alias für `Invoke-WebRequest` und hat eine andere Syntax als das echte `curl`. Verwende `Invoke-RestMethod` oder `curl.exe` (mit `.exe`-Endung) für die echte curl-Syntax.

## Konfiguration

Die Konfiguration wird unter `%APPDATA%\IlsEggingenSilentPrint\appsettings.json` gespeichert.

| Einstellung  | Standard | Beschreibung            |
| ------------ | -------- | ----------------------- |
| PrinterName  | ""       | Drucker (leer=Standard) |
| Port         | 9150     | HTTP-Server Port        |
| AutoStart    | true     | Windows-Autostart       |
| MarginTop    | 3        | Rand oben (mm)          |
| MarginBottom | 3        | Rand unten (mm)         |
| MarginLeft   | 3        | Rand links (mm)         |
| MarginRight  | 3        | Rand rechts (mm)        |

## Lizenz

Proprietär – ILS Eggingen

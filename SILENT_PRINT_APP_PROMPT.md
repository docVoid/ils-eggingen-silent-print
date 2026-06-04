# GitHub Copilot Prompt: ILS Eggingen Silent Print Application

## Projektbeschreibung

Erstelle eine Windows Desktop-Anwendung (C# / .NET 9) namens **"ILS Eggingen Silent Print"**, die als lokaler HTTP-Server läuft und Druckaufträge von einer Web-Applikation empfängt und diese ohne Benutzerinteraktion (silent) auf einem vorkonfigurierten Drucker ausgibt.

## Technologie-Stack

- **Sprache**: C# (.NET 9)
- **Projekt-Typ**: Windows Forms Application (WinForms) – für System Tray Icon
- **HTTP-Server**: ASP.NET Core Minimal API (self-hosted, Kestrel) auf `http://localhost:9150`
- **HTML-Rendering**: WebView2 (Microsoft.Web.WebView2) für PDF-Rendering des HTML
- **Drucken**: System.Drawing.Printing / WebView2 Print API
- **Konfiguration**: appsettings.json + UI-Dialog für Druckerauswahl
- **Verpackung**: Single-file executable (self-contained, publish als .exe)

## Architektur

```
ils-eggingen-silent-print/
├── Program.cs                    # Entry point, starts WinForms + Kestrel
├── PrintServer.cs                # Minimal API endpoints
├── PrintService.cs               # HTML rendering + silent print logic
├── PrintConfiguration.cs         # Drucker-Konfiguration Model
├── ConfigurationManager.cs       # Laden/Speichern der Konfiguration
├── TrayApplicationContext.cs     # System Tray Icon + Context Menu
├── SettingsForm.cs               # WinForms Dialog für Drucker-Auswahl
├── appsettings.json              # Default-Konfiguration
├── IlsEggingenSilentPrint.csproj # Projektdatei
└── README.md                     # Dokumentation
```

## Funktionale Anforderungen

### 1. HTTP-Server (Minimal API)

**Endpunkte:**

| Methode | Pfad      | Beschreibung                                                                  |
| ------- | --------- | ----------------------------------------------------------------------------- |
| GET     | `/health` | Health-Check. Gibt `200 OK` mit `{ "status": "ok", "printer": "..." }` zurück |
| POST    | `/print`  | Druckauftrag empfangen. Body: `{ "html": "...", "copies": 3 }`                |
| GET     | `/config` | Aktuelle Konfiguration abrufen                                                |

**POST /print Request-Body:**

```json
{
  "html": "<div style='font-family: monospace;'>...Alarmdruck HTML...</div>",
  "copies": 2
}
```

**POST /print Response:**

- `200 OK`: `{ "success": true, "message": "2 Kopien gedruckt" }`
- `400 Bad Request`: Wenn HTML leer oder copies < 1
- `500 Internal Server Error`: Wenn Druck fehlgeschlagen (z.B. Drucker offline)

**Validierung:**

- `html`: Pflichtfeld, nicht leer, max. 500KB
- `copies`: Pflichtfeld, Integer, 1-10

**CORS:**

- Erlaube alle Origins (da nur lokal erreichbar)
- Header: `Access-Control-Allow-Origin: *`
- Header: `Access-Control-Allow-Methods: GET, POST, OPTIONS`
- Header: `Access-Control-Allow-Headers: Content-Type`

### 2. Silent Print Mechanismus

1. Empfange HTML-Content über `/print` Endpoint
2. Rendere das HTML in einem unsichtbaren WebView2 Control
3. Nutze die WebView2 `PrintAsync()` Methode mit den konfigurierten Einstellungen
4. Drucke die konfigurierte Anzahl Kopien (`copies` aus dem Request)
5. Verwende den in der App konfigurierten Drucker
6. Kein Dialog, kein Fenster – alles im Hintergrund

**Print-Ablauf im Detail:**

```csharp
// Pseudocode
var printSettings = new CoreWebView2PrintSettings
{
    PrinterName = config.PrinterName,
    Copies = request.Copies,
    ShouldPrintBackgrounds = true,
    ShouldPrintHeaderAndFooter = false,
    Orientation = CoreWebView2PrintOrientation.Portrait,
    ScaleFactor = 1.0,
    PageWidth = 21.0,  // A4 cm
    PageHeight = 29.7, // A4 cm
    MarginTop = 1.0,
    MarginBottom = 1.0,
    MarginLeft = 1.0,
    MarginRight = 1.0,
};
await webView.CoreWebView2.PrintAsync(printSettings);
```

### 3. System Tray Applikation

- Starte minimiert im System Tray (kein Hauptfenster)
- **Tray Icon**: Drucker-Symbol (eingebettete Resource)
- **Tooltip**: "ILS Eggingen Silent Print – Verbunden" / "ILS Eggingen Silent Print – Drucker: [Name]"
- **Kontextmenü:**
  - "Einstellungen" → Öffnet SettingsForm
  - "Testdruck" → Druckt eine Testseite
  - Separator
  - "Über" → Version + Info
  - "Beenden" → Beendet die Applikation

### 4. Einstellungen (SettingsForm)

WinForms-Dialog mit:

- **Drucker-Auswahl**: DropDown mit allen installierten Druckern (System.Drawing.Printing.PrinterSettings.InstalledPrinters)
- **Port**: Numerisches Feld (Standard: 9150)
- **Autostart**: Checkbox – bei Windows-Start automatisch starten (Registry: HKCU\Software\Microsoft\Windows\CurrentVersion\Run)
- **Testdruck-Button**: Druckt eine Testseite auf dem ausgewählten Drucker
- **Speichern / Abbrechen** Buttons

### 5. Konfiguration (appsettings.json)

```json
{
  "PrinterName": "",
  "Port": 9150,
  "AutoStart": true,
  "MarginTop": 10,
  "MarginBottom": 10,
  "MarginLeft": 10,
  "MarginRight": 10
}
```

- Wird im `%APPDATA%\IlsEggingenSilentPrint\` Verzeichnis gespeichert
- Falls kein Drucker konfiguriert: Verwende Standard-Drucker des Systems
- Konfiguration wird beim Start geladen und bei Änderungen direkt geschrieben

### 6. Autostart

- Registriere/Entferne den Registry-Key `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\IlsEggingenSilentPrint`
- Wert: Pfad zur .exe
- Steuerbar über die Einstellungen

## Nicht-funktionale Anforderungen

### Performance

- Druckauftrag muss innerhalb von **2 Sekunden** verarbeitet werden
- WebView2 wird beim Start einmalig initialisiert und wiederverwendet (kein Neustart pro Druck)
- Der HTTP-Server muss sofort nach Start erreichbar sein

### Stabilität

- Bei Drucker-Fehlern: Fehler loggen, HTTP 500 zurückgeben, aber App nicht beenden
- Bei ungültigen Requests: HTTP 400 mit klarer Fehlermeldung
- Graceful Shutdown wenn Windows herunterfährt
- Exception-Handling auf allen Ebenen – nie crashen

### Sicherheit

- Keine Authentifizierung nötig (da nur lokal)
- HTML-Input wird nur im isolierten WebView2 gerendert (Sandbox)
- Keine Dateiablage – HTML wird nur in-memory verarbeitet
- Request-Body Size Limit: 500KB

### Logging

- Logge in eine Datei: `%APPDATA%\IlsEggingenSilentPrint\logs\print.log`
- Rotierendes Log (max. 5 MB, 3 Dateien)
- Log-Level: Information (Standard), Debug (konfigurierbar)
- Logge: Timestamp, Druckaufträge (Anzahl Kopien, Erfolg/Fehler), Start/Stop

## Code-Qualität & Prinzipien

- **SOLID Principles** durchgängig
- **Clean Code** – sprechende Namen, keine Magic Numbers
- **Dependency Injection** via Microsoft.Extensions.DependencyInjection
- **Interface-basiert**: `IPrintService`, `IConfigurationManager`
- **async/await** für alle I/O-Operationen
- **CancellationToken** Support
- **Keine God-Klassen** – max. ~100 Zeilen pro Klasse
- **Error Handling** mit Result Pattern oder spezifischen Exceptions

## NuGet Packages

```xml
<PackageReference Include="Microsoft.Web.WebView2" Version="1.*" />
<PackageReference Include="Serilog" Version="4.*" />
<PackageReference Include="Serilog.Sinks.File" Version="6.*" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="9.*" />
<PackageReference Include="Microsoft.AspNetCore.App" />
```

## Publish-Konfiguration

```xml
<PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net9.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <ApplicationIcon>printer.ico</ApplicationIcon>
    <AssemblyName>IlsSilentPrint</AssemblyName>
    <RootNamespace>IlsSilentPrint</RootNamespace>
</PropertyGroup>
```

## Testdruck-Seite (für den Testdruck-Button)

```html
<div style="font-family: monospace; padding: 20px;">
  <h1>ILS Eggingen Silent Print - Testdruck</h1>
  <p>Drucker: {PrinterName}</p>
  <p>Datum: {DateTime.Now}</p>
  <p>Port: {Port}</p>
  <hr />
  <p>Wenn Sie diese Seite sehen, funktioniert der Silent Print korrekt.</p>
</div>
```

## Vollständiger Ablauf (Sequenzdiagramm)

```
Browser (ILS Webapp)       ILS Eggingen Silent Print App     Windows Drucker
       |                              |                         |
       |-- POST /print -------------->|                         |
       |   { html, copies: 3 }       |                         |
       |                              |-- Render HTML --------->|
       |                              |   (WebView2)            |
       |                              |                         |
       |                              |-- PrintAsync() -------->|
       |                              |   (3 copies, silent)    |
       |                              |                         |
       |                              |<-- Print complete ------|
       |<-- 200 OK -------------------|                         |
       |   { success: true }          |                         |
```

## Fehlerbehandlung

| Szenario                       | Verhalten                                                |
| ------------------------------ | -------------------------------------------------------- |
| Drucker offline/nicht gefunden | HTTP 500, Log-Eintrag, App läuft weiter                  |
| Ungültiges HTML                | HTML trotzdem an WebView2 senden (rendert leer)          |
| Request ohne Body              | HTTP 400: "HTML content is required"                     |
| Copies > 10 oder < 1           | HTTP 400: "Copies must be between 1 and 10"              |
| WebView2 nicht installiert     | Fehlermeldung beim App-Start, Hinweis auf Installation   |
| Port bereits belegt            | Fehlermeldung beim Start, alternative Port-Wahl anbieten |

## Beispiel-Nutzung

Die ILS-Webapp sendet folgenden Request wenn ein Alarm ausgelöst wird:

```javascript
// Frontend-Code (bereits implementiert)
await fetch("http://localhost:9150/print", {
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify({
    html: printRef.current.innerHTML, // Alarmdruck HTML
    copies: 3,
  }),
  signal: AbortSignal.timeout(5000),
});
```

## Zusätzliche Hinweise

- Die App soll ohne Admin-Rechte installierbar/ausführbar sein
- WebView2 Runtime muss als Prerequisite installiert sein (oder Evergreen WebView2 nutzen)
- Die App soll beim ersten Start den Einstellungs-Dialog öffnen, wenn noch kein Drucker konfiguriert ist
- Windows 10/11 Kompatibilität
- Deutsche UI-Texte (Deutsch als Sprache der Benutzeroberfläche)

Aktuell wird die hauptapp unter localhost:3000 ausgeführt. später wird sie aber im web gehostet z.b. ils.ulm-eggingen.de, diese verbindung sollte ebenfalls funktionieren.

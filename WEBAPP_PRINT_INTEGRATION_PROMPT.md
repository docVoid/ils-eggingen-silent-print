# GitHub Copilot Prompt: ILS Eggingen Webapp – Silent Print Integration

## Aufgabe

Implementiere in der ILS Eggingen Webapp eine **Konfigurationsseite** und einen **Print-Service**, um Alarmdrucke über die lokale **ILS Eggingen Silent Print App** (Desktop-Anwendung) auszuführen. Die Silent Print App läuft als lokaler HTTP-Server auf dem PC des Benutzers.

## Kontext

Die **ILS Eggingen Silent Print App** ist eine Windows-Desktop-Anwendung, die als HTTP-Server auf dem lokalen Rechner des Benutzers läuft. Sie empfängt HTML-Inhalte per REST-API und druckt diese ohne Dialog auf einem konfigurierten Drucker.

### Wie es funktioniert

```
Browser (ILS Webapp)         Silent Print App (lokal auf PC)       Drucker
       |                              |                              |
       |-- POST /print -------------->|                              |
       |   { html, copies }          |-- Render HTML (WebView2) --->|
       |                              |-- PrintAsync (silent) ----->|
       |                              |                              |
       |<-- 200 OK -------------------|<-- Print complete -----------|
       |   { success: true }          |                              |
```

**Wichtig:** Die Webapp (egal ob `localhost:3000` oder `ils.ulm-eggingen.de`) sendet die Druckaufträge an die **lokale IP/localhost des PCs**, auf dem der Browser läuft. Die Silent Print App läuft auf dem PC des Benutzers, nicht auf dem Webserver!

### API der Silent Print App

| Methode | Pfad      | Beschreibung                                                  |
| ------- | --------- | ------------------------------------------------------------- |
| GET     | `/health` | Health-Check → `{ "status": "ok", "printer": "Druckername" }` |
| POST    | `/print`  | Druckauftrag → Body: `{ "html": "...", "copies": 3 }`         |
| GET     | `/config` | Konfiguration der Print App abrufen                           |

**POST /print Request:**

```json
{
  "html": "<div style='font-family: monospace;'>...Alarmdruck HTML...</div>",
  "copies": 2
}
```

**POST /print Response:**

- `200 OK`: `{ "success": true, "message": "2 Kopie(n) erfolgreich gedruckt" }`
- `400 Bad Request`: `{ "success": false, "message": "HTML content is required" }`
- `500 Internal Server Error`: Drucker offline oder Fehler

**Validierung durch die Print App:**

- `html`: Pflichtfeld, nicht leer, max. 500 KB
- `copies`: Pflichtfeld, Integer, 1–10

## Anforderungen an die Webapp

### 1. Konfigurationsseite (Einstellungen)

Erstelle eine Konfigurationsseite/Section in den Einstellungen der Webapp mit folgenden Feldern:

| Feld                   | Typ    | Standard                | Beschreibung                                 |
| ---------------------- | ------ | ----------------------- | -------------------------------------------- |
| **Silent Print aktiv** | Toggle | `false`                 | Aktiviert/deaktiviert den Silent Print       |
| **Print Server URL**   | Text   | `http://localhost:9150` | Basis-URL der Silent Print App               |
| **Anzahl Kopien**      | Number | `3`                     | Standard-Anzahl Kopien bei Alarmdruck (1–10) |

**Hinweise zur URL-Konfiguration:**

- Standard: `http://localhost:9150` (wenn Webapp und Print App auf dem gleichen PC laufen)
- Wenn die Webapp im Web gehostet wird (z.B. `ils.ulm-eggingen.de`), muss der Benutzer `http://localhost:9150` eintragen – die Anfrage geht vom **Browser des Benutzers** direkt an die lokale Print App
- Alternative: Lokale IP des PCs, z.B. `http://192.168.1.100:9150` (wenn ein anderer PC drucken soll)

**Verbindungstest-Button:**

- Button "Verbindung testen" neben der URL
- Führt `GET /health` auf die konfigurierte URL aus
- Zeigt Ergebnis an: "Verbunden – Drucker: [Name]" oder "Nicht erreichbar"
- Timeout: 3 Sekunden

### 2. Print-Service (Frontend)

Erstelle einen Service/Hook der den Druckauftrag an die Silent Print App sendet:

```typescript
// Beispiel-Interface
interface SilentPrintConfig {
  enabled: boolean;
  serverUrl: string; // z.B. "http://localhost:9150"
  copies: number; // 1-10
}

interface PrintResult {
  success: boolean;
  message: string;
}

// Service-Funktionen die implementiert werden sollen:

// Health-Check / Verbindungstest
async function checkPrintConnection(
  serverUrl: string,
): Promise<{ status: string; printer: string }>;

// Druckauftrag senden
async function sendPrintJob(
  config: SilentPrintConfig,
  htmlContent: string,
): Promise<PrintResult>;
```

**Implementierung:**

```typescript
async function sendPrintJob(
  config: SilentPrintConfig,
  htmlContent: string,
): Promise<PrintResult> {
  if (!config.enabled) {
    return { success: false, message: "Silent Print ist deaktiviert" };
  }

  try {
    const response = await fetch(`${config.serverUrl}/print`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        html: htmlContent,
        copies: config.copies,
      }),
      signal: AbortSignal.timeout(10000), // 10s Timeout
    });

    const data = await response.json();

    if (!response.ok) {
      return {
        success: false,
        message: data.message || `HTTP ${response.status}`,
      };
    }

    return data;
  } catch (error) {
    if (error instanceof DOMException && error.name === "TimeoutError") {
      return {
        success: false,
        message: "Silent Print App nicht erreichbar (Timeout)",
      };
    }
    if (error instanceof TypeError) {
      return {
        success: false,
        message: "Silent Print App nicht erreichbar. Läuft die App?",
      };
    }
    return { success: false, message: `Druckfehler: ${error}` };
  }
}
```

### 3. Alarmdruck-Integration

Beim Auslösen eines Alarms soll automatisch der Alarmdruck gesendet werden:

```typescript
// Beim Alarm-Event:
async function onAlarmTriggered(alarmData: AlarmData) {
  const printConfig = loadSilentPrintConfig(); // Aus den Einstellungen laden

  if (!printConfig.enabled) return;

  // HTML des Alarmdrucks erstellen (bereits vorhandene Logik)
  const printHtml = renderAlarmPrint(alarmData);

  // An Silent Print App senden
  const result = await sendPrintJob(printConfig, printHtml);

  if (!result.success) {
    // Fehler als Notification/Toast anzeigen
    showNotification(`Druckfehler: ${result.message}`, "error");
  }
}
```

### 4. Konfiguration speichern

Die Print-Konfiguration soll im gleichen Konfigurationssystem der Webapp gespeichert werden (localStorage, Datenbank, o.ä.):

```json
{
  "silentPrint": {
    "enabled": false,
    "serverUrl": "http://localhost:9150",
    "copies": 3
  }
}
```

## Fehlerbehandlung

| Szenario                     | Verhalten in der Webapp                                 |
| ---------------------------- | ------------------------------------------------------- |
| Print App nicht erreichbar   | Toast/Notification: "Silent Print App nicht erreichbar" |
| Print App gibt 400 zurück    | Toast mit Fehlermeldung der App                         |
| Print App gibt 500 zurück    | Toast: "Druckfehler – Drucker prüfen"                   |
| Timeout (>10s)               | Toast: "Silent Print Timeout – App prüfen"              |
| Silent Print deaktiviert     | Kein Druckversuch, kein Fehler                          |
| Mixed Content (HTTPS → HTTP) | Siehe Hinweis unten                                     |

## Wichtig: Mixed Content Problem

Wenn die Webapp über **HTTPS** gehostet wird (z.B. `https://ils.ulm-eggingen.de`), aber die Silent Print App nur **HTTP** nutzt (`http://localhost:9150`), blockieren Browser normalerweise die Anfrage ("Mixed Content").

**Lösung:** Browser erlauben `http://localhost` als Ausnahme von der Mixed Content Policy. Requests an `http://localhost:*` und `http://127.0.0.1:*` werden von Chrome, Edge und Firefox akzeptiert, auch wenn die Seite über HTTPS geladen wurde.

**Deshalb:** Die Print Server URL sollte immer `http://localhost:9150` verwenden (nicht `http://127.0.0.1:9150` oder die LAN-IP), um diese Browser-Ausnahme zu nutzen.

## UI-Texte (Deutsch)

- Überschrift: "Silent Print Konfiguration"
- Toggle-Label: "Silent Print aktivieren"
- URL-Label: "Print Server URL"
- URL-Placeholder: "http://localhost:9150"
- URL-Hinweis: "URL der lokalen Silent Print App. Standard: http://localhost:9150"
- Kopien-Label: "Anzahl Kopien"
- Test-Button: "Verbindung testen"
- Test-Erfolg: "✓ Verbunden – Drucker: {name}"
- Test-Fehler: "✗ Nicht erreichbar"
- Speichern: "Speichern"

using IlsSilentPrint;
using Serilog;

namespace IlsSilentPrint;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        // Serilog konfigurieren
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IlsEggingenSilentPrint", "logs");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logDirectory, "print.log"),
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: 5 * 1024 * 1024,
                retainedFileCountLimit: 3,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("=== ILS Eggingen Silent Print startet ===");

        try
        {
            // WebView2 Verfügbarkeit prüfen
            string? webView2Version = null;
            try
            {
                webView2Version = Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString();
                Log.Information("WebView2 Runtime gefunden: {Version}", webView2Version);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "WebView2 Runtime nicht verfügbar");
                MessageBox.Show(
                    "WebView2 Runtime ist nicht installiert oder nicht verfügbar.\n\n" +
                    "Bitte installieren Sie die WebView2 Runtime:\nhttps://developer.microsoft.com/en-us/microsoft-edge/webview2/\n\n" +
                    $"Fehler: {ex.Message}",
                    "ILS Eggingen Silent Print – Fehler",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            // Konfiguration laden
            var configManager = new AppConfigurationManager();
            var config = configManager.Load();

            // PrintService erstellen und initialisieren
            var printService = new PrintService(configManager);

            try
            {
                printService.InitializeSync();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "WebView2 konnte nicht initialisiert werden");
                MessageBox.Show(
                    "WebView2 konnte nicht initialisiert werden.\n\n" +
                    $"Runtime-Version: {webView2Version}\n\n" +
                    $"Fehler: {ex.Message}",
                    "ILS Eggingen Silent Print – Fehler",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            // Beim ersten Start (kein Drucker konfiguriert) Einstellungen öffnen
            if (string.IsNullOrWhiteSpace(config.PrinterName))
            {
                Log.Information("Kein Drucker konfiguriert, öffne Einstellungen");
                var settingsForm = new SettingsForm(configManager, printService);
                settingsForm.ShowDialog();
                config = configManager.Load();
            }

            // CancellationTokenSource für die gesamte Anwendung
            using var appCts = new CancellationTokenSource();

            // Kestrel Web-Server im Hintergrund starten
            var webServerTask = Task.Run(() => StartWebServerAsync(configManager, printService, config.Port, appCts.Token));

            // System Tray starten (blockiert bis Beenden)
            var trayContext = new TrayApplicationContext(configManager, printService, appCts);
            Application.Run(trayContext);

            // Auf Server-Shutdown warten
            appCts.Cancel();
            webServerTask.GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Unbehandelter Fehler in der Anwendung");
            MessageBox.Show($"Kritischer Fehler: {ex.Message}", "ILS Eggingen Silent Print",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Log.Information("=== ILS Eggingen Silent Print beendet ===");
            Log.CloseAndFlush();
        }
    }

    private static async Task StartWebServerAsync(
        IPrintConfigurationManager configManager,
        IPrintService printService,
        int port,
        CancellationToken cancellationToken)
    {
        try
        {
            var builder = WebApplication.CreateBuilder();

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenLocalhost(port);
                options.Limits.MaxRequestBodySize = 512_000;
            });

            builder.Services.AddSingleton<IPrintConfigurationManager>(configManager);
            builder.Services.AddSingleton<IPrintService>(printService);

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });

            builder.Logging.ClearProviders();
            builder.Services.AddSerilog();

            var app = builder.Build();

            app.UseCors();
            PrintServer.MapEndpoints(app);

            Log.Information("HTTP-Server startet auf Port {Port}", port);
            await app.RunAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Log.Information("HTTP-Server wurde beendet");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Fehler beim Starten des HTTP-Servers auf Port {Port}", port);

            MessageBox.Show(
                $"Der HTTP-Server konnte nicht auf Port {port} gestartet werden.\n\n" +
                $"Möglicherweise ist der Port bereits belegt.\n\nFehler: {ex.Message}",
                "ILS Eggingen Silent Print – Server-Fehler",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}

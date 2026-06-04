using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Serilog;

namespace IlsSilentPrint;

public sealed class PrintService : IPrintService
{
    private readonly IPrintConfigurationManager _configManager;
    private WebView2? _webView;
    private Form? _hostForm;
    private readonly SemaphoreSlim _printSemaphore = new(1, 1);
    private bool _isInitialized;

    public bool IsInitialized => _isInitialized;

    public PrintService(IPrintConfigurationManager configManager)
    {
        _configManager = configManager;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        _hostForm = new Form
        {
            Width = 1,
            Height = 1,
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.None,
            WindowState = FormWindowState.Minimized,
            Opacity = 0
        };
        _hostForm.Show();
        _hostForm.Hide();

        _webView = new WebView2
        {
            Dock = DockStyle.Fill
        };
        _hostForm.Controls.Add(_webView);

        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IlsEggingenSilentPrint", "WebView2Data");

        Directory.CreateDirectory(userDataFolder);

        var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
        await _webView.EnsureCoreWebView2Async(env);

        _isInitialized = true;
        Log.Information("WebView2 erfolgreich initialisiert");
    }

    /// <summary>
    /// Synchrone Initialisierung für den STA-Thread.
    /// Nutzt eine temporäre Nachrichtenpumpe, um Deadlocks zu vermeiden.
    /// </summary>
    public void InitializeSync()
    {
        if (_isInitialized) return;

        Exception? initException = null;

        var initForm = new Form
        {
            Width = 1,
            Height = 1,
            ShowInTaskbar = false,
            FormBorderStyle = FormBorderStyle.None,
            Opacity = 0
        };

        initForm.Shown += async (_, _) =>
        {
            try
            {
                await InitializeAsync();
            }
            catch (Exception ex)
            {
                initException = ex;
            }
            finally
            {
                initForm.Close();
            }
        };

        Application.Run(initForm);

        if (initException is not null)
        {
            throw initException;
        }
    }

    public async Task<PrintResponse> PrintHtmlAsync(string html, int copies, CancellationToken cancellationToken = default)
    {
        if (!_isInitialized || _webView?.CoreWebView2 is null || _hostForm is null)
        {
            return PrintResponse.Error("WebView2 ist nicht initialisiert");
        }

        await _printSemaphore.WaitAsync(cancellationToken);
        try
        {
            // WebView2 muss auf dem UI-Thread bedient werden.
            // Wenn wir von einem anderen Thread kommen (z.B. Kestrel), per Invoke weiterleiten.
            if (_hostForm.InvokeRequired)
            {
                return await RunOnUiThreadAsync(() => ExecutePrintAsync(html, copies, cancellationToken));
            }

            return await ExecutePrintAsync(html, copies, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Log.Warning("Druckauftrag wurde abgebrochen");
            return PrintResponse.Error("Druckauftrag wurde abgebrochen");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Fehler beim Drucken");
            return PrintResponse.Error($"Druckfehler: {ex.Message}");
        }
        finally
        {
            _printSemaphore.Release();
        }
    }

    private async Task<PrintResponse> ExecutePrintAsync(string html, int copies, CancellationToken cancellationToken)
    {
        var config = _configManager.Load();
        var printerName = GetEffectivePrinterName(config);

        Log.Information("Druckauftrag gestartet: {Copies} Kopie(n) auf Drucker '{Printer}'", copies, printerName);

        var navigationTcs = new TaskCompletionSource();
        void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
                navigationTcs.TrySetResult();
            else
                navigationTcs.TrySetException(new InvalidOperationException($"Navigation fehlgeschlagen: {e.WebErrorStatus}"));
        }

        _webView!.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
        try
        {
            _webView.CoreWebView2.NavigateToString(html);

            using var navigationCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            navigationCts.CancelAfter(TimeSpan.FromSeconds(10));

            await navigationTcs.Task.WaitAsync(navigationCts.Token);
        }
        finally
        {
            _webView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
        }

        // Kurz warten, damit das Rendering abgeschlossen ist
        await Task.Delay(200, cancellationToken);

        var printSettings = _webView.CoreWebView2.Environment.CreatePrintSettings();
        printSettings.PrinterName = printerName;
        printSettings.Copies = copies;
        printSettings.ShouldPrintBackgrounds = true;
        printSettings.ShouldPrintHeaderAndFooter = false;
        printSettings.Orientation = CoreWebView2PrintOrientation.Portrait;
        printSettings.ScaleFactor = 1.0;
        printSettings.PageWidth = 21.0;
        printSettings.PageHeight = 29.7;
        printSettings.MarginTop = config.MarginTop / 10.0;
        printSettings.MarginBottom = config.MarginBottom / 10.0;
        printSettings.MarginLeft = config.MarginLeft / 10.0;
        printSettings.MarginRight = config.MarginRight / 10.0;

        var result = await _webView.CoreWebView2.PrintAsync(printSettings);

        if (result == CoreWebView2PrintStatus.Succeeded)
        {
            var message = $"{copies} Kopie(n) erfolgreich gedruckt";
            Log.Information(message);
            return PrintResponse.Ok(message);
        }
        else
        {
            var message = $"Druck fehlgeschlagen: {result}";
            Log.Error(message);
            return PrintResponse.Error(message);
        }
    }

    private Task<PrintResponse> RunOnUiThreadAsync(Func<Task<PrintResponse>> action)
    {
        var tcs = new TaskCompletionSource<PrintResponse>();

        _hostForm!.BeginInvoke(async () =>
        {
            try
            {
                var result = await action();
                tcs.TrySetResult(result);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });

        return tcs.Task;
    }

    private static string GetEffectivePrinterName(PrintConfiguration config)
    {
        if (!string.IsNullOrWhiteSpace(config.PrinterName))
            return config.PrinterName;

        var defaultPrinter = new System.Drawing.Printing.PrinterSettings().PrinterName;
        Log.Information("Kein Drucker konfiguriert, verwende Standarddrucker: {Printer}", defaultPrinter);
        return defaultPrinter;
    }

    public void Dispose()
    {
        _printSemaphore.Dispose();
        _webView?.Dispose();
        _hostForm?.Dispose();
    }
}

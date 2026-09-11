using System.Collections.Concurrent;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Serilog;

namespace IlsEggingenSilentPrint;

public sealed class PrintService : IPrintService
{
    private readonly IPrintConfigurationManager _configManager;
    private WebView2? _webView;
    private Form? _hostForm;
    private readonly ConcurrentQueue<PrintJob> _printQueue = new();
    private System.Windows.Forms.Timer? _queueTimer;
    private bool _processingQueue;
    private bool _isInitialized;

    private sealed record PrintJob(
        TaskCompletionSource<PrintResponse> Tcs,
        string Html,
        int Copies,
        CancellationToken CancellationToken);

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

        _queueTimer = new System.Windows.Forms.Timer { Interval = 150 };
        _queueTimer.Tick += OnQueueTimerTick;
        _queueTimer.Start();

        _isInitialized = true;
        Log.Information("WebView2 erfolgreich initialisiert");
    }

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

    public Task<PrintResponse> PrintHtmlAsync(string html, int copies, CancellationToken cancellationToken = default)
    {
        if (!_isInitialized || _webView?.CoreWebView2 is null || _hostForm is null)
        {
            return Task.FromResult(PrintResponse.Error("WebView2 ist nicht initialisiert"));
        }

        var tcs = new TaskCompletionSource<PrintResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        _printQueue.Enqueue(new PrintJob(tcs, html, copies, cancellationToken));
        return tcs.Task;
    }

    private async void OnQueueTimerTick(object? sender, EventArgs e)
    {
        if (_processingQueue) return;
        _processingQueue = true;
        try
        {
            while (_printQueue.TryDequeue(out var job))
            {
                if (job.CancellationToken.IsCancellationRequested)
                {
                    job.Tcs.TrySetResult(PrintResponse.Error("Druckauftrag wurde abgebrochen"));
                    continue;
                }

                try
                {
                    var result = await ExecutePrintAsync(job.Html, job.Copies, job.CancellationToken);
                    job.Tcs.TrySetResult(result);
                }
                catch (OperationCanceledException)
                {
                    Log.Warning("Druckauftrag wurde abgebrochen");
                    job.Tcs.TrySetResult(PrintResponse.Error("Druckauftrag wurde abgebrochen"));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Fehler beim Drucken");
                    job.Tcs.TrySetResult(PrintResponse.Error($"Druckfehler: {ex.Message}"));
                }
            }
        }
        finally
        {
            _processingQueue = false;
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
        _queueTimer?.Stop();
        _queueTimer?.Dispose();
        _webView?.Dispose();
        _hostForm?.Dispose();
    }
}

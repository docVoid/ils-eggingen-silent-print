using Serilog;

namespace IlsSilentPrint;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly IPrintConfigurationManager _configManager;
    private readonly IPrintService _printService;
    private readonly CancellationTokenSource _appCts;

    public TrayApplicationContext(
        IPrintConfigurationManager configManager,
        IPrintService printService,
        CancellationTokenSource appCts)
    {
        _configManager = configManager;
        _printService = printService;
        _appCts = appCts;

        var config = configManager.Load();
        var printerDisplay = string.IsNullOrWhiteSpace(config.PrinterName)
            ? "Standard"
            : config.PrinterName;

        _trayIcon = new NotifyIcon
        {
            Icon = CreatePrinterIcon(),
            Text = $"ILS Eggingen Silent Print – Drucker: {Truncate(printerDisplay, 40)}",
            Visible = true,
            ContextMenuStrip = CreateContextMenu()
        };

        _trayIcon.DoubleClick += (_, _) => ShowSettings();
    }

    private static Icon CreatePrinterIcon()
    {
        // Erstelle ein einfaches Drucker-Symbol programmatisch
        var bitmap = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bitmap);
        g.Clear(Color.Transparent);

        // Drucker-Körper
        g.FillRectangle(Brushes.DarkSlateGray, 4, 10, 24, 12);
        // Papier oben
        g.FillRectangle(Brushes.White, 8, 4, 16, 8);
        g.DrawRectangle(Pens.Gray, 8, 4, 16, 8);
        // Papier unten (Ausgabe)
        g.FillRectangle(Brushes.White, 8, 20, 16, 8);
        g.DrawRectangle(Pens.Gray, 8, 20, 16, 8);
        // Rahmen
        g.DrawRectangle(Pens.Black, 4, 10, 24, 12);

        var handle = bitmap.GetHicon();
        return Icon.FromHandle(handle);
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        var settingsItem = new ToolStripMenuItem("Einstellungen");
        settingsItem.Click += (_, _) => ShowSettings();
        menu.Items.Add(settingsItem);

        var testPrintItem = new ToolStripMenuItem("Testdruck");
        testPrintItem.Click += async (_, _) => await ExecuteTestPrint();
        menu.Items.Add(testPrintItem);

        menu.Items.Add(new ToolStripSeparator());

        var aboutItem = new ToolStripMenuItem("Über");
        aboutItem.Click += (_, _) => ShowAbout();
        menu.Items.Add(aboutItem);

        var exitItem = new ToolStripMenuItem("Beenden");
        exitItem.Click += (_, _) => ExitApplication();
        menu.Items.Add(exitItem);

        return menu;
    }

    private void ShowSettings()
    {
        var form = new SettingsForm(_configManager, _printService);
        form.FormClosed += (_, _) => UpdateTooltip();
        form.ShowDialog();
    }

    private async Task ExecuteTestPrint()
    {
        var config = _configManager.Load();
        var printerName = string.IsNullOrWhiteSpace(config.PrinterName)
            ? "Standard"
            : config.PrinterName;

        var testHtml = $@"
<div style='font-family: monospace; padding: 20px;'>
    <h1>ILS Eggingen Silent Print - Testdruck</h1>
    <p>Drucker: {System.Security.SecurityElement.Escape(printerName)}</p>
    <p>Datum: {DateTime.Now:dd.MM.yyyy HH:mm:ss}</p>
    <p>Port: {config.Port}</p>
    <hr/>
    <p>Wenn Sie diese Seite sehen, funktioniert der Silent Print korrekt.</p>
</div>";

        try
        {
            var result = await _printService.PrintHtmlAsync(testHtml, 1);
            var icon = result.Success ? ToolTipIcon.Info : ToolTipIcon.Error;
            _trayIcon.ShowBalloonTip(3000, "Testdruck", result.Message, icon);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Fehler beim Testdruck");
            _trayIcon.ShowBalloonTip(3000, "Testdruck", $"Fehler: {ex.Message}", ToolTipIcon.Error);
        }
    }

    private void UpdateTooltip()
    {
        var config = _configManager.Load();
        var printerDisplay = string.IsNullOrWhiteSpace(config.PrinterName)
            ? "Standard"
            : config.PrinterName;
        _trayIcon.Text = $"ILS Eggingen Silent Print – Drucker: {Truncate(printerDisplay, 40)}";
    }

    private static void ShowAbout()
    {
        MessageBox.Show(
            "ILS Eggingen Silent Print\n\nVersion 1.0.0\n\nLokaler Druckserver für die ILS Eggingen Webapp.\nDruckt Alarmdrucke ohne Benutzerinteraktion.",
            "Über ILS Eggingen Silent Print",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ExitApplication()
    {
        Log.Information("Anwendung wird beendet");
        _trayIcon.Visible = false;
        _appCts.Cancel();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
    }
}

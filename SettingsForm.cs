using System.Drawing.Printing;
using Serilog;

namespace IlsEggingenSilentPrint;

public sealed class SettingsForm : Form
{
    private readonly IPrintConfigurationManager _configManager;
    private readonly IPrintService _printService;

    private readonly ComboBox _printerComboBox;
    private readonly NumericUpDown _portNumeric;
    private readonly CheckBox _autoStartCheckBox;
    private readonly NumericUpDown _marginTopNumeric;
    private readonly NumericUpDown _marginBottomNumeric;
    private readonly NumericUpDown _marginLeftNumeric;
    private readonly NumericUpDown _marginRightNumeric;
    private readonly Button _testPrintButton;
    private readonly Button _saveButton;
    private readonly Button _cancelButton;

    public SettingsForm(IPrintConfigurationManager configManager, IPrintService printService)
    {
        _configManager = configManager;
        _printService = printService;

        Text = "ILS Eggingen Silent Print – Einstellungen";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(450, 420);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            ColumnCount = 2,
            RowCount = 10,
            AutoSize = true
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;

        // Drucker
        layout.Controls.Add(CreateLabel("Drucker:"), 0, row);
        _printerComboBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        layout.Controls.Add(_printerComboBox, 1, row++);

        // Port
        layout.Controls.Add(CreateLabel("Port:"), 0, row);
        _portNumeric = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 1024, Maximum = 65535, Value = 9150 };
        layout.Controls.Add(_portNumeric, 1, row++);

        // Autostart
        layout.Controls.Add(CreateLabel("Autostart:"), 0, row);
        _autoStartCheckBox = new CheckBox { Text = "Bei Windows-Start automatisch starten", Dock = DockStyle.Fill };
        layout.Controls.Add(_autoStartCheckBox, 1, row++);

        // Margins
        layout.Controls.Add(CreateLabel("Rand oben (mm):"), 0, row);
        _marginTopNumeric = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 0, Maximum = 50, Value = 10 };
        layout.Controls.Add(_marginTopNumeric, 1, row++);

        layout.Controls.Add(CreateLabel("Rand unten (mm):"), 0, row);
        _marginBottomNumeric = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 0, Maximum = 50, Value = 10 };
        layout.Controls.Add(_marginBottomNumeric, 1, row++);

        layout.Controls.Add(CreateLabel("Rand links (mm):"), 0, row);
        _marginLeftNumeric = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 0, Maximum = 50, Value = 10 };
        layout.Controls.Add(_marginLeftNumeric, 1, row++);

        layout.Controls.Add(CreateLabel("Rand rechts (mm):"), 0, row);
        _marginRightNumeric = new NumericUpDown { Dock = DockStyle.Fill, Minimum = 0, Maximum = 50, Value = 10 };
        layout.Controls.Add(_marginRightNumeric, 1, row++);

        // Testdruck
        _testPrintButton = new Button { Text = "Testdruck", Dock = DockStyle.Fill, Height = 35 };
        _testPrintButton.Click += OnTestPrintClick;
        layout.Controls.Add(_testPrintButton, 0, row);
        layout.SetColumnSpan(_testPrintButton, 2);
        row++;

        // Buttons
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };

        _cancelButton = new Button { Text = "Abbrechen", Width = 100, Height = 35 };
        _cancelButton.Click += (_, _) => Close();
        buttonPanel.Controls.Add(_cancelButton);

        _saveButton = new Button { Text = "Speichern", Width = 100, Height = 35 };
        _saveButton.Click += OnSaveClick;
        buttonPanel.Controls.Add(_saveButton);

        layout.Controls.Add(buttonPanel, 0, row);
        layout.SetColumnSpan(buttonPanel, 2);

        Controls.Add(layout);
        LoadPrinters();
        LoadConfiguration();
    }

    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill
        };
    }

    private void LoadPrinters()
    {
        _printerComboBox.Items.Clear();
        foreach (string printer in PrinterSettings.InstalledPrinters)
        {
            _printerComboBox.Items.Add(printer);
        }
    }

    private void LoadConfiguration()
    {
        var config = _configManager.Load();

        if (!string.IsNullOrWhiteSpace(config.PrinterName) && _printerComboBox.Items.Contains(config.PrinterName))
        {
            _printerComboBox.SelectedItem = config.PrinterName;
        }
        else if (_printerComboBox.Items.Count > 0)
        {
            var defaultPrinter = new PrinterSettings().PrinterName;
            var idx = _printerComboBox.Items.IndexOf(defaultPrinter);
            _printerComboBox.SelectedIndex = idx >= 0 ? idx : 0;
        }

        _portNumeric.Value = config.Port;
        _autoStartCheckBox.Checked = config.AutoStart;
        _marginTopNumeric.Value = (decimal)config.MarginTop;
        _marginBottomNumeric.Value = (decimal)config.MarginBottom;
        _marginLeftNumeric.Value = (decimal)config.MarginLeft;
        _marginRightNumeric.Value = (decimal)config.MarginRight;
    }

    private void OnSaveClick(object? sender, EventArgs e)
    {
        var config = new PrintConfiguration
        {
            PrinterName = _printerComboBox.SelectedItem?.ToString() ?? string.Empty,
            Port = (int)_portNumeric.Value,
            AutoStart = _autoStartCheckBox.Checked,
            MarginTop = (double)_marginTopNumeric.Value,
            MarginBottom = (double)_marginBottomNumeric.Value,
            MarginLeft = (double)_marginLeftNumeric.Value,
            MarginRight = (double)_marginRightNumeric.Value
        };

        _configManager.Save(config);
        AutoStartManager.SetAutoStart(config.AutoStart);

        Log.Information("Einstellungen gespeichert");
        MessageBox.Show("Einstellungen wurden gespeichert.\nPort-Änderungen werden erst nach einem Neustart wirksam.",
            "Gespeichert", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Close();
    }

    private async void OnTestPrintClick(object? sender, EventArgs e)
    {
        var printerName = _printerComboBox.SelectedItem?.ToString() ?? "Standard";
        var port = (int)_portNumeric.Value;

        var testHtml = $@"
<div style='font-family: monospace; padding: 20px;'>
    <h1>ILS Eggingen Silent Print - Testdruck</h1>
    <p>Drucker: {System.Security.SecurityElement.Escape(printerName)}</p>
    <p>Datum: {DateTime.Now:dd.MM.yyyy HH:mm:ss}</p>
    <p>Port: {port}</p>
    <hr/>
    <p>Wenn Sie diese Seite sehen, funktioniert der Silent Print korrekt.</p>
</div>";

        _testPrintButton.Enabled = false;
        _testPrintButton.Text = "Druckt...";

        try
        {
            var result = await _printService.PrintHtmlAsync(testHtml, 1);

            if (result.Success)
            {
                MessageBox.Show("Testdruck wurde erfolgreich gesendet.", "Testdruck",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show($"Testdruck fehlgeschlagen: {result.Message}", "Fehler",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Fehler beim Testdruck");
            MessageBox.Show($"Testdruck fehlgeschlagen: {ex.Message}", "Fehler",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _testPrintButton.Enabled = true;
            _testPrintButton.Text = "Testdruck";
        }
    }
}

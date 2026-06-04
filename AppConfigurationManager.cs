using System.Text.Json;
using Serilog;

namespace IlsSilentPrint;

public sealed class AppConfigurationManager : IPrintConfigurationManager
{
    private static readonly string ConfigDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "IlsEggingenSilentPrint");

    private static readonly string ConfigFilePath =
        Path.Combine(ConfigDirectory, "appsettings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public PrintConfiguration Load()
    {
        try
        {
            if (!File.Exists(ConfigFilePath))
            {
                Log.Information("Keine Konfigurationsdatei gefunden, verwende Standardwerte");
                var defaults = new PrintConfiguration();
                Save(defaults);
                return defaults;
            }

            var json = File.ReadAllText(ConfigFilePath);
            var config = JsonSerializer.Deserialize<PrintConfiguration>(json, JsonOptions);
            Log.Information("Konfiguration geladen: Drucker={PrinterName}, Port={Port}", config?.PrinterName, config?.Port);
            return config ?? new PrintConfiguration();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Fehler beim Laden der Konfiguration");
            return new PrintConfiguration();
        }
    }

    public void Save(PrintConfiguration configuration)
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            var json = JsonSerializer.Serialize(configuration, JsonOptions);
            File.WriteAllText(ConfigFilePath, json);
            Log.Information("Konfiguration gespeichert: Drucker={PrinterName}, Port={Port}", configuration.PrinterName, configuration.Port);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Fehler beim Speichern der Konfiguration");
        }
    }
}

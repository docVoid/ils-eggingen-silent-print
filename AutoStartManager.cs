using Microsoft.Win32;
using Serilog;

namespace IlsEggingenSilentPrint;

public static class AutoStartManager
{
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "IlsEggingenSilentPrint";

    public static void SetAutoStart(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
            if (key is null) return;

            if (enabled)
            {
                var exePath = Environment.ProcessPath ?? Application.ExecutablePath;
                key.SetValue(AppName, $"\"{exePath}\"");
                Log.Information("Autostart aktiviert: {Path}", exePath);
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
                Log.Information("Autostart deaktiviert");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Fehler beim Setzen des Autostarts");
        }
    }

    public static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            return key?.GetValue(AppName) is not null;
        }
        catch
        {
            return false;
        }
    }
}

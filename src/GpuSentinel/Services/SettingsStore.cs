using System.Text.Json;
using GpuSentinel.Models;

namespace GpuSentinel.Services;

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AppSettings Load()
    {
        AppPaths.EnsureCreated();
        try
        {
            if (!File.Exists(AppPaths.SettingsFile))
                return new AppSettings();

            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile), JsonOptions)
                           ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write("Could not load settings; defaults will be used", exception);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        AppPaths.EnsureCreated();
        settings.Normalize();
        var temporaryFile = AppPaths.SettingsFile + ".tmp";
        File.WriteAllText(temporaryFile, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporaryFile, AppPaths.SettingsFile, true);
    }
}

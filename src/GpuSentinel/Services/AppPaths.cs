namespace GpuSentinel.Services;

public static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GpuSentinel");

    public static string LogsDirectory { get; } = Path.Combine(DataDirectory, "logs");
    public static string SettingsFile { get; } = Path.Combine(DataDirectory, "settings.json");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}

using System.Text;

namespace GpuSentinel.Services;

public static class DiagnosticLog
{
    private static readonly object Sync = new();

    public static void Write(string message, Exception? exception = null)
    {
        try
        {
            AppPaths.EnsureCreated();
            var text = new StringBuilder()
                .Append(DateTimeOffset.Now.ToString("O"))
                .Append(" | ")
                .Append(message);

            if (exception is not null)
                text.Append(" | ").Append(exception.GetType().Name).Append(": ").Append(exception.Message);

            lock (Sync)
                File.AppendAllText(Path.Combine(AppPaths.LogsDirectory, "diagnostics.log"), text.AppendLine().ToString());
        }
        catch
        {
            // Logging must never take down the monitor.
        }
    }
}

using System.Globalization;
using GpuSentinel.Models;

namespace GpuSentinel.Services;

public sealed class CsvTelemetryLogger
{
    private static readonly string Header =
        "timestamp,gpu_index,gpu_name,temperature_c,utilization_percent,memory_used_mib,memory_total_mib,memory_percent,power_draw_w,power_limit_w,power_percent,fan_percent";

    private DateOnly? _lastCleanupDate;

    public void Append(GpuSnapshot snapshot)
    {
        try
        {
            AppPaths.EnsureCreated();
            var path = Path.Combine(AppPaths.LogsDirectory, $"gpu-{snapshot.Timestamp:yyyy-MM-dd}.csv");
            if (!File.Exists(path))
                File.AppendAllText(path, Header + Environment.NewLine);

            var c = CultureInfo.InvariantCulture;
            var row = string.Join(',', new[]
            {
                snapshot.Timestamp.ToString("O", c), snapshot.Index.ToString(c), Escape(snapshot.Name),
                snapshot.TemperatureC.ToString("0.##", c), snapshot.UtilizationPercent.ToString("0.##", c),
                snapshot.MemoryUsedMiB.ToString("0.##", c), snapshot.MemoryTotalMiB.ToString("0.##", c),
                snapshot.MemoryPercent.ToString("0.##", c), snapshot.PowerDrawWatts.ToString("0.##", c),
                snapshot.PowerLimitWatts.ToString("0.##", c), snapshot.PowerPercent.ToString("0.##", c),
                snapshot.FanPercent.ToString("0.##", c)
            });
            File.AppendAllText(path, row + Environment.NewLine);

            CleanupOldLogs(snapshot.Timestamp);
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write("Could not append telemetry log", exception);
        }
    }

    private void CleanupOldLogs(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.LocalDateTime);
        if (_lastCleanupDate == today)
            return;

        _lastCleanupDate = today;
        foreach (var file in Directory.EnumerateFiles(AppPaths.LogsDirectory, "gpu-*.csv"))
        {
            if (File.GetLastWriteTimeUtc(file) < now.UtcDateTime.AddDays(-30))
                File.Delete(file);
        }
    }

    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}

using GpuSentinel.Models;

namespace GpuSentinel.Monitoring;

public static class AlertEvaluator
{
    public static AlertAssessment Evaluate(GpuSnapshot snapshot, AppSettings settings)
    {
        if (snapshot.TemperatureC >= settings.CriticalTemperatureC)
            return new(AlertLevel.Critical, $"Critical temperature: {snapshot.TemperatureC:0}°C");

        if (snapshot.MemoryPercent >= settings.CriticalMemoryPercent)
            return new(AlertLevel.Critical, $"VRAM almost full: {snapshot.MemoryPercent:0}%");

        if (snapshot.TemperatureC >= settings.WarningTemperatureC)
            return new(AlertLevel.Warning, $"GPU is running hot: {snapshot.TemperatureC:0}°C");

        if (snapshot.MemoryPercent >= settings.WarningMemoryPercent)
            return new(AlertLevel.Warning, $"VRAM usage is high: {snapshot.MemoryPercent:0}%");

        if (snapshot.UtilizationPercent >= settings.HighLoadPercent)
            return new(AlertLevel.HighLoad, $"Heavy workload: {snapshot.UtilizationPercent:0}%");

        return new(AlertLevel.Normal, "All readings normal");
    }
}

namespace GpuSentinel.Models;

public sealed record GpuSnapshot(
    DateTimeOffset Timestamp,
    int Index,
    string Name,
    double TemperatureC,
    double UtilizationPercent,
    double MemoryUsedMiB,
    double MemoryTotalMiB,
    double PowerDrawWatts,
    double PowerLimitWatts,
    double FanPercent)
{
    public double MemoryPercent => MemoryTotalMiB <= 0 ? 0 : MemoryUsedMiB / MemoryTotalMiB * 100;
    public double PowerPercent => PowerLimitWatts <= 0 ? 0 : PowerDrawWatts / PowerLimitWatts * 100;
}

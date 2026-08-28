namespace GpuSentinel.Models;

public sealed class AppSettings
{
    public int GpuIndex { get; set; } = 0;
    public int PollIntervalSeconds { get; set; } = 2;
    public int WarningTemperatureC { get; set; } = 80;
    public int CriticalTemperatureC { get; set; } = 88;
    public int WarningMemoryPercent { get; set; } = 90;
    public int CriticalMemoryPercent { get; set; } = 97;
    public int HighLoadPercent { get; set; } = 95;
    public bool SoundEnabled { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public double OverlayOpacity { get; set; } = 0.94;
    public int? WindowLeft { get; set; }
    public int? WindowTop { get; set; }

    public void Normalize()
    {
        GpuIndex = Math.Max(0, GpuIndex);
        PollIntervalSeconds = Math.Clamp(PollIntervalSeconds, 1, 30);
        WarningTemperatureC = Math.Clamp(WarningTemperatureC, 40, 100);
        CriticalTemperatureC = Math.Clamp(CriticalTemperatureC, WarningTemperatureC + 1, 110);
        WarningMemoryPercent = Math.Clamp(WarningMemoryPercent, 50, 99);
        CriticalMemoryPercent = Math.Clamp(CriticalMemoryPercent, WarningMemoryPercent + 1, 100);
        HighLoadPercent = Math.Clamp(HighLoadPercent, 50, 100);
        OverlayOpacity = Math.Clamp(OverlayOpacity, 0.55, 1.0);
    }
}

namespace GpuSentinel.Monitoring;

public enum AlertLevel
{
    Normal = 0,
    HighLoad = 1,
    Warning = 2,
    Critical = 3,
    Offline = 4
}

public sealed record AlertAssessment(AlertLevel Level, string Message);

using GpuSentinel.Models;
using GpuSentinel.Monitoring;
using GpuSentinel.Services;

var tests = new (string Name, Action Run)[]
{
    ("NVIDIA output parses", NvidiaOutputParses),
    ("Alert thresholds classify readings", AlertThresholdsClassifyReadings),
    ("Critical alerts require two samples", CriticalAlertsRequireTwoSamples),
    ("Recovery requires five samples", RecoveryRequiresFiveSamples),
    ("Settings normalize unsafe values", SettingsNormalizeUnsafeValues)
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL  {test.Name}: {exception.Message}");
    }
}

Console.WriteLine($"{tests.Length - failed}/{tests.Length} checks passed");
return failed == 0 ? 0 : 1;

static void NvidiaOutputParses()
{
    const string sample = "0, NVIDIA GeForce RTX 5090, 67, 98, 24576, 32607, 510.25, 575.00, 72\r\n";
    var snapshot = NvidiaSmiProvider.Parse(sample, DateTimeOffset.UnixEpoch).Single();
    Assert(snapshot.Index == 0, "GPU index was not parsed.");
    Assert(snapshot.Name == "NVIDIA GeForce RTX 5090", "GPU name was not parsed.");
    Assert(Math.Abs(snapshot.MemoryPercent - 75.37) < 0.01, "VRAM percentage is incorrect.");
    Assert(Math.Abs(snapshot.PowerPercent - 88.74) < 0.01, "Power percentage is incorrect.");
}

static void AlertThresholdsClassifyReadings()
{
    var settings = new AppSettings();
    Assert(AlertEvaluator.Evaluate(Snapshot(79, 50, 50), settings).Level == AlertLevel.Normal, "Normal reading was not normal.");
    Assert(AlertEvaluator.Evaluate(Snapshot(80, 50, 50), settings).Level == AlertLevel.Warning, "Hot reading did not warn.");
    Assert(AlertEvaluator.Evaluate(Snapshot(88, 50, 50), settings).Level == AlertLevel.Critical, "Critical heat was missed.");
    Assert(AlertEvaluator.Evaluate(Snapshot(60, 99, 50), settings).Level == AlertLevel.HighLoad, "Heavy load was not identified.");
    Assert(AlertEvaluator.Evaluate(Snapshot(60, 50, 98), settings).Level == AlertLevel.Critical, "Critical VRAM was missed.");
}

static void CriticalAlertsRequireTwoSamples()
{
    var state = new AlertStateMachine();
    var critical = new AlertAssessment(AlertLevel.Critical, "Critical");
    Assert(!state.Push(critical), "Critical state changed after one reading.");
    Assert(state.Current.Level == AlertLevel.Normal, "State changed too soon.");
    Assert(state.Push(critical), "Critical state did not change after two readings.");
    Assert(state.Current.Level == AlertLevel.Critical, "Critical state was not retained.");
}

static void RecoveryRequiresFiveSamples()
{
    var state = new AlertStateMachine();
    var warning = new AlertAssessment(AlertLevel.Warning, "Warning");
    state.Push(warning); state.Push(warning); state.Push(warning);
    var normal = new AlertAssessment(AlertLevel.Normal, "Normal");
    for (var i = 0; i < 4; i++)
        Assert(!state.Push(normal), "State recovered before five readings.");
    Assert(state.Push(normal), "State did not recover after five readings.");
}

static void SettingsNormalizeUnsafeValues()
{
    var settings = new AppSettings
    {
        PollIntervalSeconds = 0,
        WarningTemperatureC = 100,
        CriticalTemperatureC = 50,
        WarningMemoryPercent = 99,
        CriticalMemoryPercent = 70,
        OverlayOpacity = 0.1
    };
    settings.Normalize();
    Assert(settings.PollIntervalSeconds == 1, "Polling floor was not applied.");
    Assert(settings.CriticalTemperatureC > settings.WarningTemperatureC, "Temperature thresholds overlap.");
    Assert(settings.CriticalMemoryPercent > settings.WarningMemoryPercent, "VRAM thresholds overlap.");
    Assert(settings.OverlayOpacity == 0.55, "Opacity floor was not applied.");
}

static GpuSnapshot Snapshot(double temperature, double utilization, double memoryPercent)
{
    return new GpuSnapshot(DateTimeOffset.Now, 0, "Test GPU", temperature, utilization,
        memoryPercent, 100, 100, 500, 50);
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

using System.Diagnostics;
using System.Globalization;
using GpuSentinel.Models;

namespace GpuSentinel.Services;

public sealed class NvidiaSmiProvider
{
    private const string Fields =
        "index,name,temperature.gpu,utilization.gpu,memory.used,memory.total,power.draw,power.limit,fan.speed";

    public async Task<GpuSnapshot> ReadAsync(int gpuIndex, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "nvidia-smi.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.StartInfo.ArgumentList.Add($"--query-gpu={Fields}");
        process.StartInfo.ArgumentList.Add("--format=csv,noheader,nounits");

        try
        {
            if (!process.Start())
                throw new InvalidOperationException("NVIDIA telemetry could not be started.");

            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.WaitForExitAsync(timeout.Token);
            var output = await outputTask;
            var error = await errorTask;

            if (process.ExitCode != 0)
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "NVIDIA telemetry returned an error." : error.Trim());

            var snapshots = Parse(output, DateTimeOffset.Now);
            return snapshots.FirstOrDefault(snapshot => snapshot.Index == gpuIndex)
                   ?? throw new InvalidOperationException($"GPU {gpuIndex} was not reported by the NVIDIA driver.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw new TimeoutException("NVIDIA telemetry did not respond within five seconds.");
        }
        catch
        {
            TryKill(process);
            throw;
        }
    }

    public static IReadOnlyList<GpuSnapshot> Parse(string output, DateTimeOffset timestamp)
    {
        var result = new List<GpuSnapshot>();
        foreach (var rawLine in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var columns = rawLine.Split(',', StringSplitOptions.TrimEntries);
            if (columns.Length != 9)
                throw new FormatException($"Unexpected NVIDIA telemetry format ({columns.Length} fields).");

            result.Add(new GpuSnapshot(timestamp,
                ParseInt(columns[0], "GPU index"), columns[1],
                ParseDouble(columns[2], "temperature"), ParseDouble(columns[3], "utilization"),
                ParseDouble(columns[4], "memory used"), ParseDouble(columns[5], "memory total"),
                ParseDouble(columns[6], "power draw"), ParseDouble(columns[7], "power limit"),
                ParseOptionalDouble(columns[8])));
        }

        if (result.Count == 0)
            throw new FormatException("The NVIDIA driver returned no GPU readings.");
        return result;
    }

    private static int ParseInt(string value, string field) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result : throw new FormatException($"Invalid {field} value: {value}");

    private static double ParseDouble(string value, string field) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result)
            ? result : throw new FormatException($"Invalid {field} value: {value}");

    private static double ParseOptionalDouble(string value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0;

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(true);
        }
        catch
        {
            // Process cleanup is best effort.
        }
    }
}

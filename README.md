# GPU Sentinel

[![Build](https://github.com/OG2507/GPUMonitor/actions/workflows/build.yml/badge.svg)](https://github.com/OG2507/GPUMonitor/actions/workflows/build.yml)

GPU Sentinel is a small, always-on-top Windows overlay for keeping an eye on an NVIDIA GPU while you work. It shows temperature, GPU load, VRAM usage, and power draw; warns when heat or memory pressure becomes unsafe; and keeps a rolling telemetry history for diagnosing crashes.

It is intentionally simple: put it in a corner, move it whenever you like, and otherwise forget about it until a reading needs your attention.

## Download

Download the latest `GpuSentinel-win-x64.zip` from [GitHub Releases](https://github.com/OG2507/GPUMonitor/releases), extract it, and run `GpuSentinel.exe`. No installer or administrator access is required.

Because community builds are not yet code-signed, Windows SmartScreen may ask you to confirm the first launch.

## What it does

- Sits unobtrusively in the corner of any monitor and stays above other windows.
- Uses NVIDIA's own driver telemetry (`nvidia-smi`)—no background hardware service or administrator access required.
- Shows green, blue, amber, red, and grey states for normal, heavy workload, warning, critical, and unavailable readings.
- Requires consecutive unsafe readings before escalating, and several safe readings before clearing an alert.
- Plays an optional sound and shows a Windows notification for warning and critical conditions.
- Logs readings to daily CSV files and automatically retains the last 30 days.
- Remembers its monitor position, supports adjustable opacity and thresholds, and can start with Windows.
- Continues running in the notification area if the overlay is hidden with Alt+F4.

## Requirements

- Windows 10 or 11, 64-bit
- An NVIDIA GPU with a current NVIDIA driver

GPU Sentinel currently supports NVIDIA GPUs. AMD and Intel support are possible future additions.

## Run from source

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), then:

```powershell
dotnet run --project src/GpuSentinel/GpuSentinel.csproj
```

Right-click the overlay or its notification-area icon to open settings, pause monitoring, find logs, or exit. Drag anywhere on the overlay to move it.

## Build a portable Windows release

```powershell
dotnet publish src/GpuSentinel/GpuSentinel.csproj `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  --output artifacts/GpuSentinel-win-x64
```

The resulting `GpuSentinel.exe` can be copied to another Windows PC without separately installing .NET. Unsigned community builds may trigger Windows SmartScreen; code signing can be added to official releases later.

## Safety behaviour

The defaults are intentionally conservative:

| Reading | Warning | Critical |
| --- | ---: | ---: |
| GPU temperature | 80°C | 88°C |
| VRAM used | 90% | 97% |

High GPU utilisation is shown in blue as a heavy workload, not as a fault. Modern GPUs are designed to operate at full utilisation. Power draw is displayed and highlighted at 95% of the configured power limit, but does not by itself trigger a danger alert.

An overlay cannot prevent every driver, power-supply, cooling, or hardware failure. GPU Sentinel provides early warning and evidence; it does not change clocks, voltage, fan control, or terminate workloads.

## Data and privacy

All data stays on the PC. Settings and logs are stored under:

```text
%LOCALAPPDATA%\GpuSentinel
```

No analytics, network requests, or automatic uploads are included.

## Development

```powershell
dotnet build GpuSentinel.sln --configuration Release
dotnet run --project tests/GpuSentinel.SmokeTests/GpuSentinel.SmokeTests.csproj --configuration Release
```

Contributions and issue reports are welcome. Please include `diagnostics.log` and the relevant telemetry CSV when reporting a monitoring problem, after reviewing them for anything you do not wish to share.

## License

[MIT](LICENSE)

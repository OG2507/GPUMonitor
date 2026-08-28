using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Media;
using GpuSentinel.Models;
using GpuSentinel.Monitoring;
using GpuSentinel.Services;

namespace GpuSentinel.UI;

public sealed class OverlayForm : Form
{
    private readonly SettingsStore _settingsStore;
    private readonly NvidiaSmiProvider _provider = new();
    private readonly CsvTelemetryLogger _telemetryLogger = new();
    private readonly AlertStateMachine _stateMachine = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _pauseMenuItem;
    private readonly Label _titleLabel;
    private readonly Label _statusLabel;
    private readonly Panel _statusDot;
    private readonly MeterRow _temperatureRow;
    private readonly MeterRow _loadRow;
    private readonly MeterRow _memoryRow;
    private readonly MeterRow _powerRow;
    private AppSettings _settings;
    private bool _polling;
    private bool _paused;
    private bool _allowClose;
    private Point _dragOrigin;
    private Point _windowOrigin;
    private bool _dragging;
    private AlertLevel _displayedLevel = AlertLevel.Normal;
    private DateTimeOffset _lastAlertAt = DateTimeOffset.MinValue;
    private string? _lastLoggedError;

    public OverlayForm(AppSettings settings, SettingsStore settingsStore)
    {
        _settings = settings;
        _settingsStore = settingsStore;

        Text = "GPU Sentinel";
        Icon = SystemIcons.Shield;
        ClientSize = new Size(300, 211);
        MinimumSize = MaximumSize = Size;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.FromArgb(24, 29, 38);
        Opacity = _settings.OverlayOpacity;
        DoubleBuffered = true;
        Padding = new Padding(14, 10, 14, 10);
        AccessibleName = "GPU Sentinel monitoring overlay";

        _titleLabel = new Label
        {
            Text = "GPU SENTINEL",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(239, 243, 248),
            Location = new Point(14, 10)
        };
        _statusDot = new Panel { Size = new Size(9, 9), Location = new Point(277, 15), BackColor = Accent(AlertLevel.Normal) };
        _statusLabel = new Label
        {
            Text = "Starting monitor…",
            AutoEllipsis = true,
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.FromArgb(164, 176, 192),
            Location = new Point(14, 32),
            Size = new Size(272, 18)
        };

        _temperatureRow = CreateRow(52);
        _loadRow = CreateRow(84);
        _memoryRow = CreateRow(116);
        _powerRow = CreateRow(148);

        var hintLabel = new Label
        {
            Text = "Drag to move  •  Right-click for options",
            Font = new Font("Segoe UI", 7.5F),
            ForeColor = Color.FromArgb(108, 119, 135),
            Location = new Point(14, 188),
            Size = new Size(272, 15),
            TextAlign = ContentAlignment.MiddleCenter
        };

        Controls.AddRange(new Control[]
        {
            _titleLabel, _statusDot, _statusLabel,
            _temperatureRow, _loadRow, _memoryRow, _powerRow, hintLabel
        });

        _pauseMenuItem = new ToolStripMenuItem("Pause monitoring", null, (_, _) => TogglePause());
        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings…", null, (_, _) => ShowSettings());
        menu.Items.Add(_pauseMenuItem);
        menu.Items.Add("Open logs", null, (_, _) => OpenLogs());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit GPU Sentinel", null, (_, _) => ExitApplication());
        ContextMenuStrip = menu;
        foreach (Control control in Controls)
            control.ContextMenuStrip = menu;

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Shield,
            Text = "GPU Sentinel — starting",
            Visible = true,
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) =>
        {
            Show();
            TopMost = true;
        };

        foreach (Control control in Controls.Cast<Control>().Append(this))
        {
            control.MouseDown += BeginDrag;
            control.MouseMove += ContinueDrag;
            control.MouseUp += EndDrag;
        }

        _timer.Interval = _settings.PollIntervalSeconds * 1000;
        _timer.Tick += async (_, _) => await PollAsync();
        Shown += async (_, _) =>
        {
            RestorePosition();
            _timer.Start();
            await PollAsync();
        };
        FormClosing += OnFormClosing;
        FormClosed += (_, _) =>
        {
            _shutdown.Cancel();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _timer.Dispose();
            _shutdown.Dispose();
        };
        Resize += (_, _) => ApplyRoundedRegion();
        ApplyRoundedRegion();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int wsExToolWindow = 0x00000080;
            var parameters = base.CreateParams;
            parameters.ExStyle |= wsExToolWindow;
            return parameters;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Accent(_displayedLevel), _displayedLevel >= AlertLevel.Warning ? 2F : 1F);
        e.Graphics.DrawRoundedRectangle(pen, new RectangleF(1, 1, ClientSize.Width - 3, ClientSize.Height - 3), 12);
    }

    private MeterRow CreateRow(int top)
    {
        return new MeterRow { Location = new Point(14, top), Size = new Size(272, 31), Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
    }

    private async Task PollAsync()
    {
        if (_polling || _paused || _shutdown.IsCancellationRequested)
            return;

        _polling = true;
        try
        {
            var snapshot = await _provider.ReadAsync(_settings.GpuIndex, _shutdown.Token);
            _telemetryLogger.Append(snapshot);
            _lastLoggedError = null;

            var assessment = AlertEvaluator.Evaluate(snapshot, _settings);
            var changed = _stateMachine.Push(assessment);
            Render(snapshot, _stateMachine.Current);
            if (changed)
                NotifyIfNeeded(_stateMachine.Current);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
        catch (Exception exception)
        {
            var offline = new AlertAssessment(AlertLevel.Offline, "GPU readings unavailable");
            var changed = _stateMachine.Push(offline);
            RenderOffline(offline);
            if (_lastLoggedError != exception.Message)
            {
                DiagnosticLog.Write("GPU readings unavailable", exception);
                _lastLoggedError = exception.Message;
            }
            if (changed)
                ShowTrayMessage("GPU readings unavailable", "GPU Sentinel cannot currently read the NVIDIA driver.", ToolTipIcon.Warning);
        }
        finally
        {
            _polling = false;
        }
    }

    private void Render(GpuSnapshot snapshot, AlertAssessment state)
    {
        _displayedLevel = state.Level;
        var accent = Accent(state.Level);
        _titleLabel.Text = ShortGpuName(snapshot.Name).ToUpperInvariant();
        _statusLabel.Text = state.Message;
        _statusLabel.ForeColor = accent;
        _statusDot.BackColor = accent;

        _temperatureRow.SetReading("TEMPERATURE", $"{snapshot.TemperatureC:0}°C", Math.Min(100, snapshot.TemperatureC),
            MetricColor(snapshot.TemperatureC, _settings.WarningTemperatureC, _settings.CriticalTemperatureC));
        _loadRow.SetReading("GPU LOAD", $"{snapshot.UtilizationPercent:0}%", snapshot.UtilizationPercent,
            snapshot.UtilizationPercent >= _settings.HighLoadPercent ? Accent(AlertLevel.HighLoad) : Accent(AlertLevel.Normal));
        _memoryRow.SetReading("VRAM", $"{snapshot.MemoryUsedMiB / 1024:0.0} / {snapshot.MemoryTotalMiB / 1024:0.0} GB",
            snapshot.MemoryPercent, MetricColor(snapshot.MemoryPercent, _settings.WarningMemoryPercent, _settings.CriticalMemoryPercent));
        _powerRow.SetReading("POWER", $"{snapshot.PowerDrawWatts:0} / {snapshot.PowerLimitWatts:0} W", snapshot.PowerPercent,
            snapshot.PowerPercent >= 95 ? Accent(AlertLevel.HighLoad) : Accent(AlertLevel.Normal));

        _trayIcon.Text = Truncate($"GPU Sentinel — {snapshot.TemperatureC:0}°C, load {snapshot.UtilizationPercent:0}%, VRAM {snapshot.MemoryPercent:0}%", 63);
        Invalidate();
    }

    private void RenderOffline(AlertAssessment state)
    {
        _displayedLevel = state.Level;
        _statusLabel.Text = state.Message;
        _statusLabel.ForeColor = Accent(state.Level);
        _statusDot.BackColor = Accent(state.Level);
        _trayIcon.Text = "GPU Sentinel — readings unavailable";
        Invalidate();
    }

    private void NotifyIfNeeded(AlertAssessment assessment)
    {
        if (assessment.Level < AlertLevel.Warning)
            return;

        if (DateTimeOffset.Now - _lastAlertAt < TimeSpan.FromSeconds(30))
            return;

        _lastAlertAt = DateTimeOffset.Now;
        if (_settings.SoundEnabled)
        {
            if (assessment.Level == AlertLevel.Critical)
                SystemSounds.Hand.Play();
            else
                SystemSounds.Exclamation.Play();
        }

        ShowTrayMessage(assessment.Level == AlertLevel.Critical ? "GPU needs attention" : "GPU warning",
            assessment.Message, assessment.Level == AlertLevel.Critical ? ToolTipIcon.Error : ToolTipIcon.Warning);
    }

    private void ShowTrayMessage(string title, string text, ToolTipIcon icon)
    {
        _trayIcon.BalloonTipTitle = title;
        _trayIcon.BalloonTipText = text;
        _trayIcon.BalloonTipIcon = icon;
        _trayIcon.ShowBalloonTip(5000);
    }

    private void TogglePause()
    {
        _paused = !_paused;
        _pauseMenuItem.Text = _paused ? "Resume monitoring" : "Pause monitoring";
        if (_paused)
        {
            _timer.Stop();
            _statusLabel.Text = "Monitoring paused";
            _statusLabel.ForeColor = Color.FromArgb(164, 176, 192);
        }
        else
        {
            _timer.Start();
            _ = PollAsync();
        }
    }

    private void ShowSettings()
    {
        using var dialog = new SettingsForm(_settings);
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _settings = dialog.Settings;
        try
        {
            AutoStartManager.SetEnabled(_settings.StartWithWindows);
            _settingsStore.Save(_settings);
            _timer.Interval = _settings.PollIntervalSeconds * 1000;
            Opacity = _settings.OverlayOpacity;
            _ = PollAsync();
        }
        catch (Exception exception)
        {
            DiagnosticLog.Write("Could not save settings", exception);
            MessageBox.Show(this, "Some settings could not be saved. See the diagnostic log for details.",
                "GPU Sentinel", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private static void OpenLogs()
    {
        AppPaths.EnsureCreated();
        Process.Start(new ProcessStartInfo(AppPaths.LogsDirectory) { UseShellExecute = true });
    }

    private void ExitApplication()
    {
        SavePosition();
        _allowClose = true;
        Close();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        if (_allowClose || eventArgs.CloseReason == CloseReason.WindowsShutDown)
            return;

        eventArgs.Cancel = true;
        Hide();
        ShowTrayMessage("GPU Sentinel is still running", "Double-click the tray icon to show the overlay.", ToolTipIcon.Info);
    }

    private void BeginDrag(object? sender, MouseEventArgs eventArgs)
    {
        if (eventArgs.Button != MouseButtons.Left)
            return;
        _dragging = true;
        _dragOrigin = Cursor.Position;
        _windowOrigin = Location;
    }

    private void ContinueDrag(object? sender, MouseEventArgs eventArgs)
    {
        if (!_dragging || eventArgs.Button != MouseButtons.Left)
            return;
        var delta = new Size(Cursor.Position.X - _dragOrigin.X, Cursor.Position.Y - _dragOrigin.Y);
        Location = _windowOrigin + delta;
    }

    private void EndDrag(object? sender, MouseEventArgs eventArgs)
    {
        if (!_dragging)
            return;
        _dragging = false;
        SavePosition();
    }

    private void RestorePosition()
    {
        if (_settings.WindowLeft.HasValue && _settings.WindowTop.HasValue)
        {
            var requested = new Rectangle(_settings.WindowLeft.Value, _settings.WindowTop.Value, Width, Height);
            if (Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(requested)))
            {
                Location = requested.Location;
                return;
            }
        }

        var screen = Screen.AllScreens.Length > 1 ? Screen.AllScreens[1] : Screen.PrimaryScreen!;
        Location = new Point(screen.WorkingArea.Right - Width - 20, screen.WorkingArea.Top + 20);
    }

    private void SavePosition()
    {
        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
        try { _settingsStore.Save(_settings); }
        catch (Exception exception) { DiagnosticLog.Write("Could not save overlay position", exception); }
    }

    private void ApplyRoundedRegion()
    {
        using var path = new GraphicsPath();
        var bounds = new Rectangle(0, 0, Width, Height);
        const int diameter = 24;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        Region = new Region(path);
    }

    private static Color Accent(AlertLevel level) => level switch
    {
        AlertLevel.Normal => Color.FromArgb(69, 196, 126),
        AlertLevel.HighLoad => Color.FromArgb(74, 163, 255),
        AlertLevel.Warning => Color.FromArgb(255, 184, 77),
        AlertLevel.Critical => Color.FromArgb(255, 84, 96),
        AlertLevel.Offline => Color.FromArgb(155, 164, 178),
        _ => Color.White
    };

    private static Color MetricColor(double value, double warning, double critical) =>
        value >= critical ? Accent(AlertLevel.Critical) : value >= warning ? Accent(AlertLevel.Warning) : Accent(AlertLevel.Normal);

    private static string ShortGpuName(string name) => name
        .Replace("NVIDIA GeForce ", string.Empty, StringComparison.OrdinalIgnoreCase)
        .Replace("NVIDIA ", string.Empty, StringComparison.OrdinalIgnoreCase);

    private static string Truncate(string value, int maximum) => value.Length <= maximum ? value : value[..maximum];
}

internal static class GraphicsExtensions
{
    public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, RectangleF bounds, float radius)
    {
        var diameter = radius * 2;
        using var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.DrawPath(pen, path);
    }
}

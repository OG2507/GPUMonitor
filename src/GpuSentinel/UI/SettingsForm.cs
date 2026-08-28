using GpuSentinel.Models;

namespace GpuSentinel.UI;

internal sealed class SettingsForm : Form
{
    private readonly NumericUpDown _pollInterval;
    private readonly NumericUpDown _warningTemperature;
    private readonly NumericUpDown _criticalTemperature;
    private readonly NumericUpDown _warningMemory;
    private readonly NumericUpDown _criticalMemory;
    private readonly NumericUpDown _highLoad;
    private readonly NumericUpDown _opacity;
    private readonly CheckBox _sound;
    private readonly CheckBox _autoStart;
    private readonly AppSettings _original;

    public AppSettings Settings { get; private set; }

    public SettingsForm(AppSettings settings)
    {
        _original = settings;
        Settings = settings;
        Text = "GPU Sentinel settings";
        Icon = SystemIcons.Shield;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(420, 410);
        Font = new Font("Segoe UI", 9F);

        var intro = new Label
        {
            Text = "Warnings require several consecutive readings to avoid false alarms. Full GPU load alone is informational; temperature and VRAM trigger warnings.",
            Location = new Point(18, 15),
            Size = new Size(384, 52),
            ForeColor = Color.FromArgb(55, 61, 70)
        };
        Controls.Add(intro);

        var table = new TableLayoutPanel
        {
            Location = new Point(18, 75),
            Size = new Size(384, 230),
            ColumnCount = 3,
            RowCount = 7,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 45));
        for (var i = 0; i < 7; i++) table.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

        _pollInterval = AddNumber(table, 0, "Read GPU every", settings.PollIntervalSeconds, 1, 30, "sec");
        _warningTemperature = AddNumber(table, 1, "Temperature warning", settings.WarningTemperatureC, 40, 100, "°C");
        _criticalTemperature = AddNumber(table, 2, "Temperature critical", settings.CriticalTemperatureC, 41, 110, "°C");
        _warningMemory = AddNumber(table, 3, "VRAM warning", settings.WarningMemoryPercent, 50, 99, "%");
        _criticalMemory = AddNumber(table, 4, "VRAM critical", settings.CriticalMemoryPercent, 51, 100, "%");
        _highLoad = AddNumber(table, 5, "Heavy-load indicator", settings.HighLoadPercent, 50, 100, "%");
        _opacity = AddNumber(table, 6, "Overlay opacity", (decimal)(settings.OverlayOpacity * 100), 55, 100, "%");
        Controls.Add(table);

        _sound = new CheckBox { Text = "Play a warning sound", Checked = settings.SoundEnabled, AutoSize = true, Location = new Point(21, 310) };
        _autoStart = new CheckBox { Text = "Start automatically with Windows", Checked = settings.StartWithWindows, AutoSize = true, Location = new Point(21, 338) };
        Controls.AddRange(new Control[] { _sound, _autoStart });

        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(232, 373), Size = new Size(80, 27) };
        var save = new Button { Text = "Save", Location = new Point(320, 373), Size = new Size(82, 27) };
        save.Click += (_, _) => SaveAndClose();
        Controls.AddRange(new Control[] { cancel, save });
        AcceptButton = save;
        CancelButton = cancel;
    }

    private static NumericUpDown AddNumber(TableLayoutPanel table, int row, string label, decimal value, decimal minimum, decimal maximum, string unit)
    {
        var caption = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left };
        var input = new NumericUpDown { Minimum = minimum, Maximum = maximum, Value = Math.Clamp(value, minimum, maximum), Width = 72, Anchor = AnchorStyles.Right };
        var suffix = new Label { Text = unit, AutoSize = true, Anchor = AnchorStyles.Left };
        table.Controls.Add(caption, 0, row);
        table.Controls.Add(input, 1, row);
        table.Controls.Add(suffix, 2, row);
        return input;
    }

    private void SaveAndClose()
    {
        if (_criticalTemperature.Value <= _warningTemperature.Value)
        {
            MessageBox.Show(this, "The critical temperature must be higher than the warning temperature.", "Check settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (_criticalMemory.Value <= _warningMemory.Value)
        {
            MessageBox.Show(this, "The critical VRAM level must be higher than the warning level.", "Check settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Settings = new AppSettings
        {
            GpuIndex = _original.GpuIndex,
            PollIntervalSeconds = (int)_pollInterval.Value,
            WarningTemperatureC = (int)_warningTemperature.Value,
            CriticalTemperatureC = (int)_criticalTemperature.Value,
            WarningMemoryPercent = (int)_warningMemory.Value,
            CriticalMemoryPercent = (int)_criticalMemory.Value,
            HighLoadPercent = (int)_highLoad.Value,
            OverlayOpacity = (double)_opacity.Value / 100,
            SoundEnabled = _sound.Checked,
            StartWithWindows = _autoStart.Checked,
            WindowLeft = _original.WindowLeft,
            WindowTop = _original.WindowTop
        };
        Settings.Normalize();
        DialogResult = DialogResult.OK;
        Close();
    }
}

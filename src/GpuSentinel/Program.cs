using GpuSentinel.Services;
using GpuSentinel.UI;

namespace GpuSentinel;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var singleInstance = new Mutex(true, @"Local\GpuSentinel.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show("GPU Sentinel is already running. Look for it on your desktop or in the system tray.",
                "GPU Sentinel", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, args) => DiagnosticLog.Write("Unhandled UI error", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            DiagnosticLog.Write("Unhandled application error", args.ExceptionObject as Exception);

        var settingsStore = new SettingsStore();
        var settings = settingsStore.Load();
        Application.Run(new OverlayForm(settings, settingsStore));
    }
}

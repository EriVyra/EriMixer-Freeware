using System.IO;
using Microsoft.Win32;

namespace EriMixer;

internal static class WindowsStartup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal static bool Enabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue("EriMixer") is string command &&
                string.Equals(command, StartupCommand, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string StartupCommand => "\"" + Path.Combine(AppContext.BaseDirectory, "EriMixer.exe") + "\" --startup";

    internal static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue("EriMixer", StartupCommand, RegistryValueKind.String);
        else key.DeleteValue("EriMixer", false);
    }
}

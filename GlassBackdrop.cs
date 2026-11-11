using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace EriMixer;

internal static class GlassBackdrop
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Margins { public int Left, Right, Top, Bottom; }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr window, ref Margins margins);

    public static void Apply(Window window, bool popup)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        var source = HwndSource.FromHwnd(handle);
        bool enabled = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621) && !SystemParameters.HighContrast && !ThemeManager.IsLight;
        try
        {
            using var settings = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (settings?.GetValue("EnableTransparency") is int value && value == 0) enabled = false;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            enabled = false;
        }

        // Native acrylic keeps controls fully opaque and retains normal WPF resizing.
        int backdrop = enabled ? 3 /* DWMSBT_TRANSIENTWINDOW */ : 1 /* DWMSBT_NONE */;
        int result = DwmSetWindowAttribute(handle, 38 /* DWMWA_SYSTEMBACKDROP_TYPE */, ref backdrop, sizeof(int));
        var margins = enabled && result >= 0
            ? new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 }
            : new Margins();
        bool glass = enabled && result >= 0 && DwmExtendFrameIntoClientArea(handle, ref margins) >= 0;
        if (!glass)
        {
            backdrop = 1;
            DwmSetWindowAttribute(handle, 38, ref backdrop, sizeof(int));
            margins = new Margins();
            DwmExtendFrameIntoClientArea(handle, ref margins);
        }
        var baseColor = ThemeManager.WindowColor;
        if (source?.CompositionTarget != null)
            source.CompositionTarget.BackgroundColor = glass ? Colors.Transparent : baseColor;

        // In dark themes, translucent acrylic keeps the current popup feel.
        // Light Mode uses an opaque background for predictable text contrast.
        window.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(
            glass ? (popup ? (byte)32 : (byte)56) : (byte)255,
            baseColor.R, baseColor.G, baseColor.B));
    }
}

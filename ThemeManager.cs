using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Runtime.InteropServices;

namespace EriMixer;

internal enum MixerTheme
{
    EriVyra,
    Vyralight,
    DarkMode,
    LightMode
}

/// <summary>
/// Centralizes the four user-selectable palettes. New settings live in their
/// own theme.json so the original 0.2.0 installation remains rollback-safe.
/// </summary>
internal static class ThemeManager
{
    private static readonly Dictionary<string, string> EriVyraPalette = new()
    {
        ["ThemeWindowBackground"] = "#19181F",
        ["ThemeText"] = "#F5EFF2",
        ["ThemeHeaderText"] = "#F5EFF2",
        ["ThemeSecondaryText"] = "#B9AEB9",
        ["ThemeQuietText"] = "#968B99",
        ["ThemeAccent"] = "#D34262",
        ["ThemeAccentLight"] = "#E39A92",
        ["ThemeHeader"] = "#4A0D13",
        ["ThemeHeaderBorder"] = "#77232A",
        ["ThemeHeaderAction"] = "#65151B",
        ["ThemeSelectorBackground"] = "#27232D",
        ["ThemeSelectorBorder"] = "#5B444E",
        ["ThemeSelectorHover"] = "#332734",
        ["ThemePanelBackground"] = "#24222C",
        ["ThemeDialogBackground"] = "#19181F",
        ["ThemeDialogPanelBackground"] = "#211F29",
        ["ThemeDialogCardBackground"] = "#292630",
        ["ThemeDialogCardBorder"] = "#403743",
        ["ThemeDialogBorder"] = "#57414D",
        ["ThemeButtonBackground"] = "#303039",
        ["ThemeButtonBorder"] = "#65545F",
        ["ThemeButtonHoverBorder"] = "#D34262",
        ["ThemeSliderTrack"] = "#46414B",
        ["ThemeSliderThumb"] = "#FFF5F8",
        ["ThemeMuteActive"] = "#842D46",
        ["ThemeInputBackground"] = "#302B37",
        ["ThemeInputBorder"] = "#65545F",
        ["ThemeToggleBackground"] = "#2C2934",
        ["ThemeToggleBorder"] = "#78616A",
        ["ThemeMenuBackground"] = "#27232D",
        ["ThemeTextOnAccent"] = "#FFF5F8",
        ["ThemeDialogSave"] = "#7A2942"
    };

    private static readonly Dictionary<MixerTheme, Dictionary<string, string>> Palettes = new()
    {
        [MixerTheme.EriVyra] = EriVyraPalette,
        [MixerTheme.Vyralight] = Override(new()
        {
            ["ThemeWindowBackground"] = "#101726",
            ["ThemeText"] = "#EAF3FF",
            ["ThemeHeaderText"] = "#EAF3FF",
            ["ThemeSecondaryText"] = "#B0C4DD",
            ["ThemeQuietText"] = "#869FBE",
            ["ThemeAccent"] = "#569DFF",
            ["ThemeAccentLight"] = "#9BCBFF",
            ["ThemeHeader"] = "#13325A",
            ["ThemeHeaderBorder"] = "#275789",
            ["ThemeHeaderAction"] = "#204773",
            ["ThemeSelectorBackground"] = "#19283D",
            ["ThemeSelectorBorder"] = "#33577B",
            ["ThemeSelectorHover"] = "#263E5C",
            ["ThemePanelBackground"] = "#1A2435",
            ["ThemeDialogBackground"] = "#111A2B",
            ["ThemeDialogPanelBackground"] = "#172236",
            ["ThemeDialogCardBackground"] = "#1C2C43",
            ["ThemeDialogCardBorder"] = "#35506F",
            ["ThemeDialogBorder"] = "#315477",
            ["ThemeButtonBackground"] = "#243650",
            ["ThemeButtonBorder"] = "#395878",
            ["ThemeButtonHoverBorder"] = "#569DFF",
            ["ThemeSliderTrack"] = "#39506B",
            ["ThemeSliderThumb"] = "#EAF3FF",
            ["ThemeMuteActive"] = "#225991",
            ["ThemeInputBackground"] = "#21334C",
            ["ThemeInputBorder"] = "#3F668C",
            ["ThemeToggleBackground"] = "#203550",
            ["ThemeToggleBorder"] = "#53739A",
            ["ThemeMenuBackground"] = "#1B2B42",
            ["ThemeTextOnAccent"] = "#FFFFFF",
            ["ThemeDialogSave"] = "#2364A7"
        }),
        [MixerTheme.DarkMode] = Override(new()
        {
            ["ThemeWindowBackground"] = "#17191D",
            ["ThemeText"] = "#F0F1F3",
            ["ThemeHeaderText"] = "#F0F1F3",
            ["ThemeSecondaryText"] = "#BDC1C8",
            ["ThemeQuietText"] = "#969DA7",
            ["ThemeAccent"] = "#BAC5D3",
            ["ThemeAccentLight"] = "#DBE2EA",
            ["ThemeHeader"] = "#262A31",
            ["ThemeHeaderBorder"] = "#4D535D",
            ["ThemeHeaderAction"] = "#363C45",
            ["ThemeSelectorBackground"] = "#252930",
            ["ThemeSelectorBorder"] = "#535B68",
            ["ThemeSelectorHover"] = "#303640",
            ["ThemePanelBackground"] = "#23262C",
            ["ThemeDialogBackground"] = "#181B20",
            ["ThemeDialogPanelBackground"] = "#22262D",
            ["ThemeDialogCardBackground"] = "#2A2F38",
            ["ThemeDialogCardBorder"] = "#4C5560",
            ["ThemeDialogBorder"] = "#5B6571",
            ["ThemeButtonBackground"] = "#303640",
            ["ThemeButtonBorder"] = "#5B6470",
            ["ThemeButtonHoverBorder"] = "#C3CDDB",
            ["ThemeSliderTrack"] = "#4A515B",
            ["ThemeSliderThumb"] = "#FFFFFF",
            ["ThemeMuteActive"] = "#596576",
            ["ThemeInputBackground"] = "#303640",
            ["ThemeInputBorder"] = "#68727E",
            ["ThemeToggleBackground"] = "#303640",
            ["ThemeToggleBorder"] = "#7B8794",
            ["ThemeMenuBackground"] = "#282D35",
            ["ThemeTextOnAccent"] = "#18202A",
            ["ThemeDialogSave"] = "#CAD3DE"
        }),
        [MixerTheme.LightMode] = Override(new()
        {
            ["ThemeWindowBackground"] = "#F1F4F8",
            ["ThemeText"] = "#192536",
            ["ThemeHeaderText"] = "#192536",
            ["ThemeSecondaryText"] = "#485C73",
            ["ThemeQuietText"] = "#60748A",
            ["ThemeAccent"] = "#316C9B",
            ["ThemeAccentLight"] = "#245D8A",
            ["ThemeHeader"] = "#E0EAF3",
            ["ThemeHeaderBorder"] = "#B3C8DC",
            ["ThemeHeaderAction"] = "#CEDFEB",
            ["ThemeSelectorBackground"] = "#FFFFFF",
            ["ThemeSelectorBorder"] = "#B8CADB",
            ["ThemeSelectorHover"] = "#E8F1F9",
            ["ThemePanelBackground"] = "#FFFFFF",
            ["ThemeDialogBackground"] = "#F1F4F8",
            ["ThemeDialogPanelBackground"] = "#FFFFFF",
            ["ThemeDialogCardBackground"] = "#EFF4F9",
            ["ThemeDialogCardBorder"] = "#CBD8E5",
            ["ThemeDialogBorder"] = "#B0C3D3",
            ["ThemeButtonBackground"] = "#E9F0F6",
            ["ThemeButtonBorder"] = "#B7C9D9",
            ["ThemeButtonHoverBorder"] = "#316C9B",
            ["ThemeSliderTrack"] = "#C6D2DE",
            ["ThemeSliderThumb"] = "#FFFFFF",
            ["ThemeMuteActive"] = "#B8D4E8",
            ["ThemeInputBackground"] = "#FFFFFF",
            ["ThemeInputBorder"] = "#B3C6D9",
            ["ThemeToggleBackground"] = "#FFFFFF",
            ["ThemeToggleBorder"] = "#8BA1B5",
            ["ThemeMenuBackground"] = "#FFFFFF",
            ["ThemeTextOnAccent"] = "#FFFFFF",
            ["ThemeDialogSave"] = "#316C9B"
        })
    };

    public static MixerTheme Current { get; private set; } = MixerTheme.EriVyra;
    public static bool IsLight => Current == MixerTheme.LightMode;

    public static System.Windows.Media.Color WindowColor => GetColor("ThemeWindowBackground");
    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "EriMixer", "theme.json");

    private static Dictionary<string, string> Override(Dictionary<string, string> values)
    {
        var result = new Dictionary<string, string>(EriVyraPalette);
        foreach (var (key, value) in values) result[key] = value;
        return result;
    }

    private static System.Windows.Media.Color GetColor(string key) =>
        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(Palettes[Current][key])!;

    public static void Initialize()
    {
        MixerTheme requested = MixerTheme.EriVyra;
        try
        {
            if (File.Exists(SettingsPath))
            {
                var name = JsonSerializer.Deserialize<string>(File.ReadAllText(SettingsPath));
                if (Enum.TryParse<MixerTheme>(name, false, out var parsed) &&
                    Enum.IsDefined(parsed))
                    requested = parsed;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            System.Diagnostics.Debug.WriteLine("Theme settings could not be read: " + ex);
        }
        Apply(requested);
    }

    private static void Apply(MixerTheme theme)
    {
        if (!Palettes.TryGetValue(theme, out var palette))
            throw new ArgumentOutOfRangeException(nameof(theme));

        foreach (var (key, hex) in palette)
            System.Windows.Application.Current.Resources[key] = new SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!);

        // The original slider style references Accent.
        System.Windows.Application.Current.Resources["Accent"] =
            System.Windows.Application.Current.Resources["ThemeAccent"];
        Current = theme;
    }

    public static bool TrySelect(MixerTheme theme, out string? error)
    {
        error = null;
        var previous = Current;
        Apply(theme);
        try
        {
            var folder = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(folder);
            File.WriteAllText(SettingsPath + ".tmp", JsonSerializer.Serialize(theme.ToString()));
            File.Move(SettingsPath + ".tmp", SettingsPath, true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Apply(previous);
            error = "The theme could not be saved: " + ex.Message;
            return false;
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, int attribute, ref int value, int size);

    public static void ApplyTitleBar(Window window)
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        int dark = IsLight ? 0 : 1;
        _ = DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
    }
}

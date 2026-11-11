using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace EriMixer;

public partial class MainWindow
{
    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        var themes = new[]
        {
            (MixerTheme.EriVyra, "EriVyra"),
            (MixerTheme.Vyralight, "Vyralight"),
            (MixerTheme.DarkMode, "Dark Mode"),
            (MixerTheme.LightMode, "Light Mode")
        };
        var popup = CreateSelectorPopup(ThemeButton, PlacementMode.Top, 180, out var options);
        var rows = new List<(MixerTheme Theme, System.Windows.Controls.Button Option)>();

        foreach (var (theme, label) in themes)
        {
            var option = CreateSelectorOption(
                label,
                ThemeManager.Current == theme,
                _ =>
                {
                    SelectTheme(theme);
                    foreach (var row in rows)
                        SetSelectorOptionState(row.Option, ThemeManager.Current == row.Theme);
                });
            rows.Add((theme, option));
            options.Children.Add(option);
        }

        (rows.FirstOrDefault(row => row.Theme == ThemeManager.Current).Option ?? rows[0].Option).Focus();
    }

    private void SelectTheme(MixerTheme theme)
    {
        if (!ThemeManager.TrySelect(theme, out var error))
        {
            DeviceSelectionStatus.Text = error ?? "Could not save theme.";
            DeviceSelectionStatus.Visibility = Visibility.Visible;
        }
        else
        {
            DeviceSelectionStatus.Visibility = Visibility.Collapsed;
        }

        ApplyCurrentTheme();
    }

    private void ApplyCurrentTheme()
    {
        ThemeManager.ApplyTitleBar(this);
        SetPopupHeader(popupMode);
        GlassBackdrop.Apply(this, popupMode);
        RefreshSessions();
    }
}

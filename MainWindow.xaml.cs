using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NAudio.CoreAudioApi;
using Forms = System.Windows.Forms;
using NAudio.CoreAudioApi.Interfaces;

namespace EriMixer;
public partial class MainWindow : Window
{
    private readonly MMDeviceEnumerator devices = new();
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly Forms.NotifyIcon tray;
    private readonly System.Drawing.Icon trayIcon;
    private PopupHotkey? hotkey;
    private bool configuringHotkey;
    private bool exiting, popupMode, refreshing, changingMode;
    private Rect desktopBounds = new(100, 100, 420, 560);
    private System.Windows.Size popupSize = new(370, 480);
    private Rect popupWorkArea;
    private readonly Dictionary<string, SessionRow> rows = new();
    private sealed record SessionRow(Border Container, Slider Slider, System.Windows.Controls.Button Mute, TextBlock Percent, List<AudioSessionControl> Sessions);

    public MainWindow()
    {
        InitializeComponent();
        SizeChanged += (_, _) =>
        {
            if (popupMode && !changingMode && WindowState == WindowState.Normal)
                popupSize = new System.Windows.Size(ActualWidth, ActualHeight);
        };
        using (var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/EriMixer.ico")).Stream)
            trayIcon = new System.Drawing.Icon(resource);
        Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(trayIcon.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
        tray = new Forms.NotifyIcon { Icon = trayIcon, Text = "EriMixer", Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open standalone window", null, (_, _) => Dispatcher.Invoke(OpenWindow));
        menu.Items.Add("Open popup", null, (_, _) => Dispatcher.Invoke(OpenPopup));
        menu.Items.Add("Hot Keys…", null, (_, _) => Dispatcher.Invoke(ConfigureHotkey));
        var startupItem = new Forms.ToolStripMenuItem("Start with Windows");
        menu.Items.Add(startupItem);
        menu.Opening += (_, _) =>
        {
            try { startupItem.Checked = WindowsStartup.Enabled; }
            catch { startupItem.Checked = false; }
        };
        startupItem.Click += (_, _) =>
        {
            try { WindowsStartup.SetEnabled(!WindowsStartup.Enabled); }
            catch (Exception ex) { System.Windows.MessageBox.Show(this, "Could not update startup settings: " + ex.Message, "EriMixer"); }
        };
        menu.Items.Add("Exit EriMixer", null, (_, _) => Dispatcher.Invoke(ExitApp));
        tray.ContextMenuStrip = menu;
        tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) Dispatcher.Invoke(() => { if (IsVisible && popupMode) Hide(); else OpenPopup(); }); };
        timer.Tick += (_, _) => { RefreshDeviceSelectors(); RefreshSessions(); };
        timer.Start();
        RefreshDeviceSelectors();
        RefreshSessions();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        ThemeManager.ApplyTitleBar(this);
        int rounded = 2;
        DwmSetWindowAttribute(hwnd, 33, ref rounded, sizeof(int));
        GlassBackdrop.Apply(this, popupMode);
        hotkey = new PopupHotkey(this,
            () => { if (!configuringHotkey) OpenPopup(); },
            () => { if (!configuringHotkey) CycleAudioDevice(DataFlow.Capture); },
            () => { if (!configuringHotkey) CycleAudioDevice(DataFlow.Render); });
        var error = hotkey.Load();
        UpdateShortcutLabel();
        if (error != null) Dispatcher.BeginInvoke(new Action(() =>
            tray.ShowBalloonTip(7000, "EriMixer hot keys", error, Forms.ToolTipIcon.Info)));
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private void UpdateShortcutLabel()
    {
        ShortcutButton.Content = "Hot Keys";
    }

    private void ConfigureHotkey()
    {
        if (hotkey == null || configuringHotkey) return;
        configuringHotkey = true;
        try
        {
            // Leave the compact popup visible behind its owned hotkey dialog.
            hotkey.BeginEditing();
            new HotkeysDialog(this, hotkey).ShowDialog();
            UpdateShortcutLabel();
        }
        finally
        {
            var error = hotkey.FinishEditing();
            if (error != null)
                tray.ShowBalloonTip(7000, "EriMixer hot keys", error, Forms.ToolTipIcon.Warning);
            configuringHotkey = false;
        }
    }

    private void Shortcut_Click(object sender, RoutedEventArgs e) => ConfigureHotkey();

    private void RefreshSessions()
    {
        try
        {
            using var device = devices.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            DeviceLabel.Text = device.FriendlyName;
            SessionsPanel.IsEnabled = true;
            SessionsPanel.Opacity = 1;
            Title = "EriMixer";
            var grouped = new Dictionary<string, List<AudioSessionControl>>();
            for (int i = 0; i < device.AudioSessionManager.Sessions.Count; i++)
            {
                var session = device.AudioSessionManager.Sessions[i];
                if (session.State == AudioSessionState.AudioSessionStateExpired) continue;
                string name;
                try { name = session.GetProcessID == 0 ? "System Sounds" : Process.GetProcessById((int)session.GetProcessID).ProcessName; }
                catch { name = $"Audio session {session.GetProcessID}"; }
                if (!grouped.TryGetValue(name, out var list)) grouped[name] = list = new();
                list.Add(session);
            }
            foreach (var old in rows.Keys.Except(grouped.Keys).ToArray())
            {
                SessionsPanel.Children.Remove(rows[old].Container);
                rows.Remove(old);
            }
            foreach (var (name, sessions) in grouped.OrderBy(x => x.Key))
            {
                if (!rows.TryGetValue(name, out var row))
                {
                    var border = new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 8) };
                    border.SetResourceReference(Border.BackgroundProperty, "ThemePanelBackground");
                    var stack = new StackPanel();
                    var title = new TextBlock { Text = FriendlyName(sessions, name), TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = name, FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) };
                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition());
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(65) });
                    var slider = new Slider { Minimum = 0, Maximum = 100, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 9, 0), IsMoveToPointEnabled = true };
                    var percent = new TextBlock { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
                    var mute = new System.Windows.Controls.Button { Content = "Mute" };
                    Grid.SetColumn(slider, 0); Grid.SetColumn(percent, 1); Grid.SetColumn(mute, 2);
                    grid.Children.Add(slider); grid.Children.Add(percent); grid.Children.Add(mute);
                    var heading = new DockPanel { Margin = new Thickness(0, 0, 0, 3) };
                    var icon = AppIcon(sessions);
                    if (icon != null) heading.Children.Add(new System.Windows.Controls.Image { Source = icon, Width = 20, Height = 20, Margin = new Thickness(0, 0, 9, 6) });
                    heading.Children.Add(title);
                    System.Windows.Automation.AutomationProperties.SetName(slider, name + " volume");
                    System.Windows.Automation.AutomationProperties.SetName(mute, name + " mute");
                    stack.Children.Add(heading); stack.Children.Add(grid); border.Child = stack;
                    row = new SessionRow(border, slider, mute, percent, new List<AudioSessionControl>());
                    rows[name] = row;
                    var current = row;
                    slider.ValueChanged += (_, _) =>
                    {
                        current.Percent.Text = $"{(int)slider.Value}%";
                        if (refreshing) return;
                        foreach (var s in current.Sessions) try { s.SimpleAudioVolume.Volume = (float)(slider.Value / 100); } catch { }
                    };
                    mute.Click += (_, _) =>
                    {
                        bool target = !current.Sessions.All(s => { try { return s.SimpleAudioVolume.Mute; } catch { return false; } });
                        foreach (var s in current.Sessions) try { s.SimpleAudioVolume.Mute = target; } catch { }
                        SetMuteState(current, target);
                    };
                    SessionsPanel.Children.Add(border);
                }
                row.Sessions.Clear(); row.Sessions.AddRange(sessions);
                var volumes = sessions.Select(s => { try { return (double)s.SimpleAudioVolume.Volume; } catch { return 0d; } }).ToArray();
                double volume = volumes.Length == 0 ? 0 : volumes.Average() * 100;
                // Avoid fighting the user while they drag a slider.
                if (!row.Slider.IsMouseCaptureWithin)
                {
                    refreshing = true;
                    try { row.Slider.Value = volume; }
                    finally { refreshing = false; }
                }
                row.Percent.Text = $"{(int)row.Slider.Value}%";
                SetMuteState(row, sessions.All(s => { try { return s.SimpleAudioVolume.Mute; } catch { return false; } }));
            }
            EmptyState.Text = "Play audio in an app to see it here.";
            EmptyState.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            Title = "EriMixer — audio unavailable";
            DeviceLabel.Text = "Audio unavailable · reconnect your output device";
            EmptyState.Text = "No audio output is available.";
            EmptyState.Visibility = Visibility.Visible;
            SessionsPanel.IsEnabled = false;
            SessionsPanel.Opacity = 0.2;
            Debug.WriteLine(ex);
        }
    }

    private static void SetMuteState(SessionRow row, bool muted)
    {
        row.Mute.Content = muted ? "Unmute" : "Mute";
        row.Mute.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, muted ? "ThemeMuteActive" : "ThemeButtonBackground");
        row.Mute.ToolTip = muted ? "Muted — click to restore audio" : "Mute this application";
        row.Slider.Opacity = muted ? 0.45 : 1;
    }

    private static string FriendlyName(List<AudioSessionControl> sessions, string fallback)
    {
        try
        {
            if (sessions[0].GetProcessID == 0) return "System Sounds";
            using var process = Process.GetProcessById((int)sessions[0].GetProcessID);
            return process.MainModule?.FileVersionInfo.FileDescription is string label && !string.IsNullOrWhiteSpace(label) ? label : fallback;
        }
        catch { return fallback; }
    }

    private static ImageSource? AppIcon(List<AudioSessionControl> sessions)
    {
        try
        {
            using var process = Process.GetProcessById((int)sessions[0].GetProcessID);
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(process.MainModule!.FileName!);
            if (icon == null) return null;
            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            source.Freeze(); return source;
        }
        catch { return null; }
    }

    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (popupMode && e.Key == System.Windows.Input.Key.Escape) { Hide(); e.Handled = true; }
        base.OnPreviewKeyDown(e);
    }

    private void OpenWindow()
    {
        popupMode = false;
        System.Windows.Shell.WindowChrome.SetWindowChrome(this, null);
        MaxWidth = double.PositiveInfinity; MaxHeight = double.PositiveInfinity;
        FitAppsButton.Visibility = Visibility.Collapsed;
        SetPopupHeader(false);
        popupMode = false; ModeButton.Content = "Popup"; WindowStyle = WindowStyle.SingleBorderWindow; ResizeMode = ResizeMode.CanResize;
        ShowInTaskbar = true; Topmost = false; Width = desktopBounds.Width; Height = desktopBounds.Height; Left = desktopBounds.Left; Top = desktopBounds.Top; Show(); WindowState = WindowState.Normal; Activate();
        GlassBackdrop.Apply(this, false);
    }
    internal void ShowStandalone() => OpenWindow();
    private void OpenPopup()
    {
        changingMode = true;
        try
        {
        if (!popupMode && IsVisible && WindowState == WindowState.Normal) desktopBounds = new Rect(Left, Top, ActualWidth, ActualHeight);
        popupMode = true; ModeButton.Content = "Expand"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResize;
        System.Windows.Shell.WindowChrome.SetWindowChrome(this, new System.Windows.Shell.WindowChrome
        {
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(7),
            GlassFrameThickness = new Thickness(-1),
            UseAeroCaptionButtons = false
        });
        FitAppsButton.Visibility = Visibility.Visible;
        SetPopupHeader(true);
        ShowInTaskbar = false; Width = popupSize.Width; Height = popupSize.Height; Topmost = true;
        var screen = Forms.Screen.FromPoint(Forms.Cursor.Position);
        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? System.Windows.Media.Matrix.Identity;
        var origin = transform.Transform(new System.Windows.Point(screen.WorkingArea.Left, screen.WorkingArea.Top));
        var size = transform.Transform(new System.Windows.Point(screen.WorkingArea.Width, screen.WorkingArea.Height));
        var area = new Rect(origin.X, origin.Y, size.X, size.Y);
        popupWorkArea = area;
        MaxWidth = Math.Max(MinWidth, area.Width - 24);
        MaxHeight = Math.Max(MinHeight, area.Height - 24);
        Width = Math.Min(Width, area.Width - 24); Height = Math.Min(Height, area.Height - 24);
        Left = area.Right - Width - 12; Top = area.Bottom - Height - 12;
        Show(); WindowState = WindowState.Normal; Activate();
        GlassBackdrop.Apply(this, true);
        }
        finally { changingMode = false; }
    }

    private void SetPopupHeader(bool popup)
    {
        // Keep the signature crimson/blue/neutral header in both window modes.
        HeaderBar.SetResourceReference(Border.BackgroundProperty, "ThemeHeader");
        HeaderBar.SetResourceReference(Border.BorderBrushProperty, "ThemeHeaderBorder");
        HeaderBar.BorderThickness = new Thickness(0, 0, 0, 1);
        HeaderBar.Margin = new Thickness(-18, -18, -18, 16);
        HeaderBar.Padding = new Thickness(18, 14, 18, 14);
        HeaderBar.CornerRadius = new CornerRadius(10, 10, 0, 0);
        ModeButton.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty, "ThemeHeaderAction");
        ModeButton.SetResourceReference(System.Windows.Controls.Control.BorderBrushProperty, "ThemeHeaderBorder");
        ModeButton.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "ThemeHeaderText");
    }

    private void FitApps_Click(object sender, RoutedEventArgs e)
    {
        if (!popupMode) return;
        UpdateLayout();
        // The stack measures all cards, including those below the viewport.
        var height = ActualHeight - SessionsScroll.ActualHeight + SessionsPanel.DesiredSize.Height + 2;
        Height = Math.Clamp(height, MinHeight, MaxHeight);
        Top = Math.Max(popupWorkArea.Top + 12, popupWorkArea.Bottom - Height - 12);
        popupSize = new System.Windows.Size(ActualWidth, Height);
    }
    private void ExitApp()
    {
        exiting = true; timer.Stop(); hotkey?.Dispose(); tray.Visible = false; tray.Dispose(); trayIcon.Dispose(); devices.Dispose(); Close();
        System.Windows.Application.Current.Shutdown();
    }
    private void Popup_Click(object sender, RoutedEventArgs e) { if (popupMode) OpenWindow(); else OpenPopup(); }
    private void Refresh_Click(object sender, RoutedEventArgs e) { RefreshDeviceSelectors(); RefreshSessions(); }
    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        ThemeManager.ApplyTitleBar(this);
        GlassBackdrop.Apply(this, popupMode);
    }
    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        if (popupMode && !changingMode && !configuringHotkey) Hide();
    }
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!exiting) { e.Cancel = true; Hide(); }
        base.OnClosing(e);
    }
}

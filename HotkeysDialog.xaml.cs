using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace EriMixer;

public partial class HotkeysDialog : Window
{
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ThemeManager.ApplyTitleBar(this);
    }

    private enum ActionKind { Input, Output, Popup }

    private readonly PopupHotkey manager;
    private bool initializing = true;
    private ActionKind? recording;
    private HotkeyChoice inputChoice;
    private HotkeyChoice outputChoice;
    private HotkeyChoice popupChoice;

    internal HotkeysDialog(Window owner, PopupHotkey manager)
    {
        InitializeComponent();
        Owner = owner;
        this.manager = manager;

        var saved = manager.Bindings;
        inputChoice = saved.Input;
        outputChoice = saved.Output;
        popupChoice = saved.Popup;
        InputEnabled.IsChecked = inputChoice.IsEnabled;
        OutputEnabled.IsChecked = outputChoice.IsEnabled;
        PopupEnabled.IsChecked = popupChoice.IsEnabled;
        initializing = false;
        RefreshLabels();
    }

    private static ActionKind KindFromTag(object? tag) =>
        Enum.Parse<ActionKind>((string)tag!, ignoreCase: false);

    private System.Windows.Controls.CheckBox ToggleFor(ActionKind kind) => kind switch
    {
        ActionKind.Input => InputEnabled,
        ActionKind.Output => OutputEnabled,
        _ => PopupEnabled
    };

    private System.Windows.Controls.Button ButtonFor(ActionKind kind) => kind switch
    {
        ActionKind.Input => InputBindButton,
        ActionKind.Output => OutputBindButton,
        _ => PopupBindButton
    };

    private HotkeyChoice ChoiceFor(ActionKind kind) => kind switch
    {
        ActionKind.Input => inputChoice,
        ActionKind.Output => outputChoice,
        _ => popupChoice
    };

    private void Assign(ActionKind kind, HotkeyChoice choice)
    {
        switch (kind)
        {
            case ActionKind.Input: inputChoice = choice; break;
            case ActionKind.Output: outputChoice = choice; break;
            case ActionKind.Popup: popupChoice = choice; break;
        }
    }

    private void RefreshLabels()
    {
        foreach (var kind in Enum.GetValues<ActionKind>())
        {
            var button = ButtonFor(kind);
            var isEnabled = ToggleFor(kind).IsChecked == true;
            var choice = ChoiceFor(kind);
            button.Content = recording == kind ? "Press shortcut…" :
                !isEnabled ? "Unbound" :
                choice.IsEnabled ? choice.Label : "Set shortcut";
            button.Opacity = isEnabled ? 1 : 0.72;
            button.ToolTip = isEnabled && choice.IsEnabled
                ? choice.Label + " · Click to change" : "Click to assign a shortcut";
        }
    }

    private void ShowStatus(string message)
    {
        StatusText.Text = message;
        StatusText.Visibility = Visibility.Visible;
    }

    private void ClearStatus()
    {
        StatusText.Text = "";
        StatusText.Visibility = Visibility.Collapsed;
    }

    private void Enabled_Changed(object sender, RoutedEventArgs e)
    {
        if (initializing) return;
        var kind = KindFromTag(((System.Windows.Controls.CheckBox)sender).Tag);
        if (recording == kind && ToggleFor(kind).IsChecked != true)
            recording = null;
        ClearStatus();
        RefreshLabels();
    }

    private void Capture_Click(object sender, RoutedEventArgs e)
    {
        var button = (System.Windows.Controls.Button)sender;
        var kind = KindFromTag(button.Tag);
        ToggleFor(kind).IsChecked = true;
        recording = kind;
        ShowStatus("Press any shortcut. Escape alone cancels recording.");
        RefreshLabels();
        button.Focus();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (recording is not ActionKind kind) return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None)
        {
            recording = null;
            ClearStatus();
            RefreshLabels();
            e.Handled = true;
            return;
        }

        // Ignore modifier-only key presses until the complete chord arrives.
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin || key == Key.None)
        {
            e.Handled = true;
            return;
        }

        var mods = Keyboard.Modifiers;
        uint native = ((mods & ModifierKeys.Alt) != 0 ? 1u : 0) |
                      ((mods & ModifierKeys.Control) != 0 ? 2u : 0) |
                      ((mods & ModifierKeys.Shift) != 0 ? 4u : 0) |
                      ((mods & ModifierKeys.Windows) != 0 ? 8u : 0);
        var candidate = new HotkeyChoice(native, KeyInterop.VirtualKeyFromKey(key));
        var error = PopupHotkey.ValidateChoice(candidate);
        if (error != null)
        {
            ShowStatus(error);
            e.Handled = true;
            return;
        }

        Assign(kind, candidate);
        recording = null;
        ClearStatus();
        RefreshLabels();
        e.Handled = true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (recording != null)
        {
            ShowStatus("Finish recording the shortcut, or press Escape to cancel recording.");
            return;
        }

        foreach (var kind in Enum.GetValues<ActionKind>())
        {
            if (ToggleFor(kind).IsChecked == true && !ChoiceFor(kind).IsEnabled)
            {
                ShowStatus("Choose a shortcut for " +
                    (kind == ActionKind.Popup ? "the popup" :
                     kind == ActionKind.Input ? "input devices" : "output devices") +
                    ", or uncheck it to leave it unbound.");
                return;
            }
        }

        var next = new HotkeyBindings(
            PopupEnabled.IsChecked == true ? popupChoice : HotkeyChoice.Disabled,
            InputEnabled.IsChecked == true ? inputChoice : HotkeyChoice.Disabled,
            OutputEnabled.IsChecked == true ? outputChoice : HotkeyChoice.Disabled);

        var error = manager.Save(next);
        if (error != null)
        {
            ShowStatus(error);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

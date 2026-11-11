using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace EriMixer;

public sealed record HotkeyChoice(uint Modifiers, int VirtualKey)
{
    public static HotkeyChoice Default => new(3, 0x4D); // Ctrl + Alt + M, preserved legacy default
    public static HotkeyChoice Disabled => new(0, 0);
    public bool IsEnabled => VirtualKey != 0;

    public string Label
    {
        get
        {
            if (!IsEnabled) return "Unbound";
            var key = KeyInterop.KeyFromVirtualKey(VirtualKey);
            string keyName = key is >= Key.NumPad0 and <= Key.NumPad9
                ? "Num " + (VirtualKey - 0x60)
                : key.ToString();
            return ((Modifiers & 2) != 0 ? "Ctrl + " : "") +
                   ((Modifiers & 1) != 0 ? "Alt + " : "") +
                   ((Modifiers & 4) != 0 ? "Shift + " : "") +
                   ((Modifiers & 8) != 0 ? "Win + " : "") + keyName;
        }
    }
}

internal sealed record HotkeyBindings(HotkeyChoice Popup, HotkeyChoice Input, HotkeyChoice Output)
{
    public static HotkeyBindings Default => new(HotkeyChoice.Default, HotkeyChoice.Disabled, HotkeyChoice.Disabled);
}

/// <summary>
/// All three shortcuts share one message-only window. The original
/// %LOCALAPPDATA%\EriMixer\settings.json remains untouched so v0.2.0 can
/// still be used as a rollback; the preview persists hotkeys.json separately.
/// </summary>
internal sealed class PopupHotkey : IDisposable
{
    private const int PopupId = 0x4552, InputId = 0x4553, OutputId = 0x4554;
    private const uint NoRepeat = 0x4000;
    private readonly HwndSource source;
    private readonly Action showPopup, cycleInput, cycleOutput;
    private readonly HashSet<int> registered = new();
    private bool editing;
    private bool disposed;

    private static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EriMixer");
    private static string HotkeysPath => Path.Combine(DirectoryPath, "hotkeys.json");
    private static string LegacyPath => Path.Combine(DirectoryPath, "settings.json");

    public HotkeyBindings Bindings { get; private set; } = HotkeyBindings.Default;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    public PopupHotkey(Window owner, Action showPopup, Action cycleInput, Action cycleOutput)
    {
        this.showPopup = showPopup;
        this.cycleInput = cycleInput;
        this.cycleOutput = cycleOutput;
        // An independent message-only HWND remains active when WPF window chrome changes.
        source = new HwndSource(new HwndSourceParameters("EriMixer global shortcuts")
        {
            ParentWindow = new IntPtr(-3)
        });
        source.AddHook(Hook);
    }

    public static string? ValidateChoice(HotkeyChoice? choice)
    {
        if (choice == null) return "A shortcut setting is missing.";
        if (!choice.IsEnabled)
            return choice.Modifiers == 0 ? null : "Unbound shortcuts must not have modifiers.";

        if (choice.VirtualKey is < 0x08 or > 0xFE || (choice.Modifiers & ~15u) != 0 ||
            choice.VirtualKey is 0x10 or 0x11 or 0x12 or 0x5B or 0x5C)
            return "That key combination is not supported.";

        // Allow any non-modifier key, with or without modifiers. Windows
        // RegisterHotKey is the authority for OS-reserved/unavailable chords.
        // A bare key may intercept ordinary typing; the user chooses deliberately.
        return null;
    }

    private static IEnumerable<(int Id, string Name, HotkeyChoice Choice)> Choices(HotkeyBindings bindings)
    {
        yield return (PopupId, "Open popup", bindings.Popup);
        yield return (InputId, "Cycle input", bindings.Input);
        yield return (OutputId, "Cycle output", bindings.Output);
    }

    private static string? ValidateBindings(HotkeyBindings? bindings)
    {
        if (bindings == null) return "Shortcut settings were missing.";
        var seen = new HashSet<(uint Modifiers, int VirtualKey)>();
        foreach (var (_, name, choice) in Choices(bindings))
        {
            var problem = ValidateChoice(choice);
            if (problem != null) return name + ": " + problem;
            if (choice.IsEnabled && !seen.Add((choice.Modifiers, choice.VirtualKey)))
                return "Two actions cannot use the same hotkey. Choose distinct shortcuts.";
        }
        return null;
    }

    public string? Load()
    {
        var errors = new List<string>();
        try
        {
            if (File.Exists(HotkeysPath))
            {
                Bindings = JsonSerializer.Deserialize<HotkeyBindings>(File.ReadAllText(HotkeysPath))
                    ?? HotkeyBindings.Default;
            }
            else if (File.Exists(LegacyPath))
            {
                var legacy = JsonSerializer.Deserialize<HotkeyChoice>(File.ReadAllText(LegacyPath));
                Bindings = HotkeyBindings.Default with { Popup = legacy ?? HotkeyChoice.Default };
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Bindings = HotkeyBindings.Default;
            errors.Add("Could not read saved hotkeys; using the default popup shortcut.");
        }

        var problem = ValidateBindings(Bindings);
        if (problem != null)
        {
            Bindings = HotkeyBindings.Default;
            errors.Add("Saved hotkeys were invalid; using the default popup shortcut.");
        }

        foreach (var (id, name, choice) in Choices(Bindings))
        {
            if (choice.IsEnabled && !Register(id, choice))
                errors.Add(name + " could not register (" + choice.Label + ").");
        }
        return errors.Count == 0 ? null : string.Join(" ", errors);
    }

    private bool Register(int id, HotkeyChoice choice)
    {
        if (!choice.IsEnabled) return true;
        if (!RegisterHotKey(source.Handle, id, choice.Modifiers | NoRepeat, (uint)choice.VirtualKey))
            return false;
        registered.Add(id);
        return true;
    }

    private void UnregisterAll()
    {
        foreach (var id in registered)
            UnregisterHotKey(source.Handle, id);
        registered.Clear();
    }

    // Temporarily free the existing shortcuts while the dialog captures keys.
    // On Cancel, FinishEditing restores the original registrations.
    public void BeginEditing()
    {
        if (editing) throw new InvalidOperationException("The hotkey editor is already open.");
        UnregisterAll();
        editing = true;
    }

    private string? TryRegisterAll(HotkeyBindings bindings)
    {
        foreach (var (id, name, choice) in Choices(bindings))
        {
            if (choice.IsEnabled && !Register(id, choice))
            {
                UnregisterAll();
                return name + " cannot use " + choice.Label +
                    ". Windows may have reserved it or another application is using it.";
            }
        }
        return null;
    }

    public string? Save(HotkeyBindings next)
    {
        if (!editing) return "Close and reopen the hotkey editor to save changes.";
        var problem = ValidateBindings(next);
        if (problem != null) return problem;

        UnregisterAll();
        problem = TryRegisterAll(next);
        if (problem != null) return problem;

        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var temp = HotkeysPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(next, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, HotkeysPath, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UnregisterAll();
            return "Could not save your shortcuts. Nothing was changed: " + ex.Message;
        }

        Bindings = next;
        editing = false;
        return null;
    }

    public string? FinishEditing()
    {
        if (!editing) return null; // Save already registered and committed the new bindings.
        editing = false;
        UnregisterAll();
        return TryRegisterAll(Bindings);
    }

    private IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != 0x0312 || editing) return IntPtr.Zero;
        Action? action = wParam.ToInt32() switch
        {
            PopupId => showPopup,
            InputId => cycleInput,
            OutputId => cycleOutput,
            _ => null
        };
        if (action != null)
        {
            handled = true;
            // Do not mutate WPF UI while Windows is dispatching WM_HOTKEY.
            source.Dispatcher.BeginInvoke(action);
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        UnregisterAll();
        source.RemoveHook(Hook);
        source.Dispose();
    }
}

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using NAudio.CoreAudioApi;

namespace EriMixer;

public partial class MainWindow
{
    // Keep endpoint IDs rather than friendly names: two devices may share a name.
    private sealed record DeviceChoice(string Id, string Name);

    private void RefreshDeviceSelectors()
    {
        try
        {
            RefreshDeviceButton(OutputDeviceButton, OutputDeviceName, DataFlow.Render);
            RefreshDeviceButton(InputDeviceButton, InputDeviceName, DataFlow.Capture);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            ShowDeviceSelectionError("Could not refresh the audio devices: " + ex.Message);
        }
    }

    private void RefreshDeviceButton(System.Windows.Controls.Button button, TextBlock label, DataFlow flow)
    {
        var choices = GetActiveDevices(flow);
        var defaultId = GetCurrentDefaultId(flow);
        var selected = choices.FirstOrDefault(d => string.Equals(d.Id, defaultId, StringComparison.Ordinal));

        label.Text = selected?.Name ?? (choices.Count == 0 ? "Not connected" : "Choose device");
        // The complete name remains accessible if it was visually truncated.
        button.ToolTip = selected?.Name ?? (flow == DataFlow.Render
            ? "Choose the default Windows output device"
            : "Choose the default Windows input device");
        button.IsEnabled = choices.Count > 0;
    }

    private List<DeviceChoice> GetActiveDevices(DataFlow flow)
    {
        var result = new List<DeviceChoice>();
        foreach (var endpoint in devices.EnumerateAudioEndPoints(flow, DeviceState.Active))
        {
            using (endpoint)
            {
                result.Add(new DeviceChoice(endpoint.ID, endpoint.FriendlyName));
            }
        }
        result.Sort((a, b) =>
        {
            int byName = StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name);
            return byName == 0 ? StringComparer.Ordinal.Compare(a.Id, b.Id) : byName;
        });
        return result;
    }

    private string? GetCurrentDefaultId(DataFlow flow)
    {
        try
        {
            using var endpoint = devices.GetDefaultAudioEndpoint(flow, Role.Multimedia);
            return endpoint.ID;
        }
        catch (Exception ex)
        {
            // A connected device does not necessarily have a Windows default.
            Debug.WriteLine(ex);
            return null;
        }
    }

    private void OutputDeviceButton_Click(object sender, RoutedEventArgs e) =>
        OpenDeviceMenu(OutputDeviceButton, DataFlow.Render);

    private void InputDeviceButton_Click(object sender, RoutedEventArgs e) =>
        OpenDeviceMenu(InputDeviceButton, DataFlow.Capture);

    private void OpenDeviceMenu(System.Windows.Controls.Button button, DataFlow flow)
    {
        try
        {
            var active = GetActiveDevices(flow);
            if (active.Count == 0)
            {
                RefreshDeviceSelectors();
                return;
            }

            var currentId = GetCurrentDefaultId(flow);
            var popup = CreateSelectorPopup(button, PlacementMode.Bottom,
                button.ActualWidth, out var options);
            var rows = new List<(DeviceChoice Device, System.Windows.Controls.Button Option)>();

            foreach (var device in active)
            {
                var option = CreateSelectorOption(
                    device.Name,
                    string.Equals(device.Id, currentId, StringComparison.Ordinal),
                    _ =>
                    {
                        SwitchDevice(flow, device);
                        var actualId = GetCurrentDefaultId(flow);
                        foreach (var row in rows)
                            SetSelectorOptionState(row.Option,
                                string.Equals(row.Device.Id, actualId, StringComparison.Ordinal));
                    });
                rows.Add((device, option));
                options.Children.Add(option);
            }

            var selected = rows.FirstOrDefault(row =>
                string.Equals(row.Device.Id, currentId, StringComparison.Ordinal)).Option;
            (selected ?? rows[0].Option).Focus();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            ShowDeviceSelectionError("Could not list audio devices: " + ex.Message);
        }
    }

    private void SwitchDevice(DataFlow flow, DeviceChoice choice)
    {
        var description = flow == DataFlow.Render ? "output" : "input";
        try
        {
            // The device might have been unplugged since the menu was opened.
            if (!GetActiveDevices(flow).Any(d => string.Equals(d.Id, choice.Id, StringComparison.Ordinal)))
                throw new InvalidOperationException("That device is no longer connected.");

            WindowsDefaultAudioDevice.SetDefault(choice.Id);
            DeviceSelectionStatus.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            ShowDeviceSelectionError("Could not switch the default " + description +
                                     " device: " + ex.Message);
        }
        finally
        {
            // Always read back Windows' actual default, rather than assuming
            // the undocumented API succeeded for every endpoint role.
            RefreshDeviceSelectors();
            RefreshSessions();
        }
    }

    private void CycleAudioDevice(DataFlow flow)
    {
        try
        {
            // Query active devices afresh for each hotkey press. This naturally
            // skips disconnected endpoints and follows the menu's sorted order.
            var choices = GetActiveDevices(flow);
            if (choices.Count < 2) return;

            var currentId = GetCurrentDefaultId(flow);
            var index = choices.FindIndex(d =>
                string.Equals(d.Id, currentId, StringComparison.Ordinal));
            // index = -1 (no default) correctly selects the first device.
            var next = choices[(index + 1) % choices.Count];
            SwitchDevice(flow, next);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            ShowDeviceSelectionError("Could not cycle " +
                (flow == DataFlow.Render ? "output" : "input") +
                " devices: " + ex.Message);
        }
    }

    private void ShowDeviceSelectionError(string message)
    {
        DeviceSelectionStatus.Text = message;
        DeviceSelectionStatus.Visibility = Visibility.Visible;
    }
}

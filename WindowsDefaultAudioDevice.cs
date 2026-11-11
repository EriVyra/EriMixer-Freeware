using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace EriMixer;

/// <summary>
/// Changes the Windows system default audio endpoint for all three endpoint roles.
/// Windows exposes supported APIs for discovering defaults, but not for setting them.
/// Keep this undocumented PolicyConfig interop isolated and test on each supported
/// Windows release before distributing an installer.
/// </summary>
internal static class WindowsDefaultAudioDevice
{
    [ComImport]
    [Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    private class PolicyConfigClient
    {
    }

    // The method order is important: COM uses the position in the vtable.
    // All methods before SetDefaultEndpoint are declared only to preserve that
    // order; EriMixer does not call them.
    [ComImport]
    [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, out IntPtr format);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int isDefault, out IntPtr format);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr endpointFormat, IntPtr mixFormat);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int isDefault, out long defaultPeriod, out long minimumPeriod);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr period);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr mode);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr mode);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int effectsStore, IntPtr key, IntPtr value);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int effectsStore, IntPtr key, IntPtr value);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, Role role);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int visible);
    }

    public static void SetDefault(string endpointId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);

        var client = new PolicyConfigClient();
        try
        {
            var policy = (IPolicyConfig)client;
            foreach (var role in new[] { Role.Console, Role.Multimedia, Role.Communications })
            {
                var result = policy.SetDefaultEndpoint(endpointId, role);
                Marshal.ThrowExceptionForHR(result);
            }
        }
        finally
        {
            if (Marshal.IsComObject(client))
                Marshal.FinalReleaseComObject(client);
        }
    }
}

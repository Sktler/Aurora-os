using System;
using System.Runtime.InteropServices;

namespace ZoeyOS.App.Services
{
    /// <summary>
    /// Gets/sets the system's master output volume via Windows' Core Audio API
    /// (the same thing the volume slider in the taskbar controls). Pure COM
    /// interop - no NuGet package, no external process, no admin rights needed.
    /// </summary>
    public static class SystemVolumeControl
    {
        public static float GetVolume()
        {
            try
            {
                var endpointVolume = GetEndpointVolume();
                endpointVolume.GetMasterVolumeLevelScalar(out var level);
                return level;
            }
            catch
            {
                return 0.5f; // best-effort default if audio interop isn't available
            }
        }

        public static void SetVolume(float level)
        {
            try
            {
                level = Math.Clamp(level, 0f, 1f);
                var endpointVolume = GetEndpointVolume();
                endpointVolume.SetMasterVolumeLevelScalar(level, Guid.Empty);
            }
            catch
            {
                // Non-fatal - a failed volume change shouldn't crash the settings window.
            }
        }

        public static bool GetMute()
        {
            try
            {
                var endpointVolume = GetEndpointVolume();
                endpointVolume.GetMute(out var muted);
                return muted;
            }
            catch
            {
                return false;
            }
        }

        public static void SetMute(bool mute)
        {
            try
            {
                var endpointVolume = GetEndpointVolume();
                endpointVolume.SetMute(mute, Guid.Empty);
            }
            catch
            {
                // Non-fatal
            }
        }

        private static IAudioEndpointVolume GetEndpointVolume()
        {
            var deviceEnumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.eRender, ERole.eMultimedia, out var device);
            var iid = typeof(IAudioEndpointVolume).GUID;
            device.Activate(ref iid, 0, IntPtr.Zero, out var epvObj);
            return (IAudioEndpointVolume)epvObj;
        }

        // --- Minimal COM interop surface for Windows Core Audio ---

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumerator { }

        private enum EDataFlow { eRender, eCapture, eAll }
        private enum ERole { eConsole, eMultimedia, eCommunications }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            int NotImpl1();
            [PreserveSig]
            int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object endpointVolume);
        }

        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            // This vtable order must match the real Windows IAudioEndpointVolume exactly -
            // COM dispatches by slot position, not by the C# method name, so a method
            // missing or out of order here makes every call after it invoke the wrong
            // native function with the wrong argument shape. That mismatch is what caused
            // the AccessViolationException: SetMute/GetMute/etc. were landing on completely
            // different real methods (GetChannelCount, SetChannelVolumeLevelScalar, ...)
            // and corrupting the stack. Full 18-method layout, per endpointvolume.h:
            int RegisterControlChangeNotify(IntPtr pNotify);
            int UnregisterControlChangeNotify(IntPtr pNotify);
            [PreserveSig] int GetChannelCount(out uint channelCount);
            [PreserveSig] int SetMasterVolumeLevel(float levelDb, Guid eventContext);
            [PreserveSig] int SetMasterVolumeLevelScalar(float level, Guid eventContext);
            [PreserveSig] int GetMasterVolumeLevel(out float levelDb);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
            [PreserveSig] int SetChannelVolumeLevel(uint channel, float levelDb, Guid eventContext);
            [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, Guid eventContext);
            [PreserveSig] int GetChannelVolumeLevel(uint channel, out float levelDb);
            [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
            [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, Guid eventContext);
            [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
            int GetVolumeStepInfo(out uint step, out uint stepCount);
            int VolumeStepUp(Guid eventContext);
            int VolumeStepDown(Guid eventContext);
            int QueryHardwareSupport(out uint hardwareSupportMask);
            int GetVolumeRange(out float volumeMinDb, out float volumeMaxDb, out float volumeIncrementDb);
        }
    }
}

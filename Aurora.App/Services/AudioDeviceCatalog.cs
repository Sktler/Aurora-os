using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace Aurora.App.Services
{
    /// <summary>One physical audio device Aurora can capture from or play through, as
    /// currently reported by Windows. <see cref="DeviceNumber"/> is only stable for the life
    /// of this device list - devices are matched back up by <see cref="Name"/> when restoring
    /// a saved selection, since Windows can renumber devices when others are plugged/unplugged.</summary>
    public record AudioDeviceOption(string Name, int DeviceNumber);

    /// <summary>Lists the microphones (capture) and speakers/headphones (render) Windows
    /// currently has available, via NAudio's WinMM device enumeration - the same catalog
    /// every other Windows app sees in its own audio device picker.</summary>
    public static class AudioDeviceCatalog
    {
        /// <summary>Sentinel used in AppSettings.MicrophoneDeviceName/SpeakerDeviceName to
        /// mean "use whatever Windows currently has set as the system default" - the
        /// long-standing behavior before device selection existed.</summary>
        public const string SystemDefaultDeviceName = "";

        public static List<AudioDeviceOption> GetInputDevices()
        {
            var devices = new List<AudioDeviceOption>();
            try
            {
                for (var i = 0; i < WaveIn.DeviceCount; i++)
                {
                    var caps = WaveIn.GetCapabilities(i);
                    devices.Add(new AudioDeviceOption(caps.ProductName, i));
                }
            }
            catch
            {
                // No capture hardware/driver available - an empty list just means the
                // Settings picker only offers "System default".
            }
            return devices;
        }

        public static List<AudioDeviceOption> GetOutputDevices()
        {
            var devices = new List<AudioDeviceOption>();
            try
            {
                for (var i = 0; i < WaveOut.DeviceCount; i++)
                {
                    var caps = WaveOut.GetCapabilities(i);
                    devices.Add(new AudioDeviceOption(caps.ProductName, i));
                }
            }
            catch
            {
                // No playback hardware/driver available - fall through to system default.
            }
            return devices;
        }

        /// <summary>Resolves a saved device name back to its current device number, or -1
        /// (meaning "use the system default") if the name is blank or no longer matches any
        /// currently connected device.</summary>
        public static int ResolveInputDeviceNumber(string? savedName) => Resolve(savedName, GetInputDevices());

        public static int ResolveOutputDeviceNumber(string? savedName) => Resolve(savedName, GetOutputDevices());

        private static int Resolve(string? savedName, List<AudioDeviceOption> devices)
        {
            if (string.IsNullOrWhiteSpace(savedName)) return -1;
            foreach (var device in devices)
                if (string.Equals(device.Name, savedName, StringComparison.OrdinalIgnoreCase))
                    return device.DeviceNumber;
            return -1;
        }
    }
}

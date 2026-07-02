using NAudio.CoreAudioApi;
using PewPewRecorder.Models;

namespace PewPewRecorder.Services;

public static class AudioDeviceEnumerator
{
    public static Task<IReadOnlyList<AudioDevice>> ListDevicesAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() => ListDevices(), cancellationToken);
    }

    private static IReadOnlyList<AudioDevice> ListDevices()
    {
        var devices = new List<AudioDevice>();

        try
        {
            using var enumerator = new MMDeviceEnumerator();

            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            {
                devices.Add(new AudioDevice
                {
                    Name = device.FriendlyName,
                    IsLoopback = true,
                    DeviceId = device.ID,
                });
                device.Dispose();
            }

            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                devices.Add(new AudioDevice
                {
                    Name = device.FriendlyName,
                    IsLoopback = false,
                    DeviceId = device.ID,
                });
                device.Dispose();
            }
        }
        catch (Exception)
        {
            // Audio enumeration can fail on some systems; UI will show a helpful message.
        }

        return devices;
    }
}

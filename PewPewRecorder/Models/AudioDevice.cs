namespace PewPewRecorder.Models;

public sealed class AudioDevice
{
    public required string Name { get; init; }
    public required string DeviceId { get; init; }
    public bool IsLoopback { get; init; }

    public override string ToString() => Name;
}

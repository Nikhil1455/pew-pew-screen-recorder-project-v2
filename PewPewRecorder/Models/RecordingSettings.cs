namespace PewPewRecorder.Models;

public sealed class RecordingSettings
{
    public int Width { get; init; }
    public int Height { get; init; }
    public int Fps { get; init; }
    public string OutputDirectory { get; init; } = string.Empty;
    public string MicrophoneDeviceId { get; init; } = string.Empty;
    public string SystemAudioDeviceId { get; init; } = string.Empty;
}

public static class ResolutionPresets
{
    public static readonly (string Label, int Width, int Height)[] Options =
    [
        ("1080p (1920×1080)", 1920, 1080),
        ("720p (1280×720)", 1280, 720),
    ];
}

public static class FpsPresets
{
    public static readonly int[] Options = [24, 30, 60];
}

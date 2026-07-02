using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PewPewRecorder.Services;

public sealed class VideoEncoder
{
    public required string Name { get; init; }
    public required string Label { get; init; }
    public required string[] Args { get; init; }
}

public static class HardwareEncoderDetector
{
    private static readonly (string Name, string Label, string[] Args, string[] Vendors)[] Priority =
    [
        ("h264_nvenc", "NVIDIA NVENC", ["-preset", "p4", "-rc", "vbr", "-cq", "20", "-b:v", "0"], ["nvidia"]),
        ("h264_amf", "AMD AMF", ["-quality", "balanced", "-rc", "cqp", "-qp_i", "20", "-qp_p", "20"], ["amd"]),
        ("h264_qsv", "Intel Quick Sync", ["-preset", "medium", "-global_quality", "20"], ["intel"]),
        ("libx264", "Software (x264)", ["-preset", "veryfast", "-crf", "18"], ["nvidia", "amd", "intel", "unknown"]),
    ];

    private static VideoEncoder? _cached;

    public static async Task<VideoEncoder> DetectAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
            return _cached;

        var gpuVendor = GpuDetector.DetectVendor();
        var available = await GetAvailableEncodersAsync(cancellationToken);

        foreach (var candidate in Priority)
        {
            if (!available.Contains(candidate.Name))
                continue;

            if (candidate.Name != "libx264" && !candidate.Vendors.Contains(gpuVendor))
                continue;

            if (candidate.Name == "libx264" || await ProbeEncoderAsync(candidate.Name, candidate.Args, cancellationToken))
            {
                _cached = new VideoEncoder
                {
                    Name = candidate.Name,
                    Label = candidate.Label,
                    Args = candidate.Args,
                };
                return _cached;
            }
        }

        _cached = new VideoEncoder
        {
            Name = "libx264",
            Label = "Software (x264)",
            Args = ["-preset", "veryfast", "-crf", "18"],
        };
        return _cached;
    }

    private static async Task<bool> ProbeEncoderAsync(string encoder, string[] args, CancellationToken cancellationToken)
    {
        if (!FfmpegLocator.IsAvailable())
            return false;

        var argStr = string.Join(' ', args);
        var probeArgs = $"-hide_banner -loglevel error -f lavfi -i color=black:s=64x64:d=0.1 -c:v {encoder} {argStr} -f null -";

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = FfmpegLocator.GetFfmpegPath(),
                Arguments = probeArgs,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null)
                return false;

            await process.WaitForExitAsync(cancellationToken);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<HashSet<string>> GetAvailableEncodersAsync(CancellationToken cancellationToken)
    {
        var encoders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!FfmpegLocator.IsAvailable())
            return encoders;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = FfmpegLocator.GetFfmpegPath(),
                Arguments = "-hide_banner -encoders",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null)
                return encoders;

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            foreach (Match match in Regex.Matches(output, @"\b(h264_nvenc|h264_amf|h264_qsv|libx264)\b"))
                encoders.Add(match.Value);
        }
        catch
        {
            // Fall back to software encoding.
        }

        return encoders;
    }
}

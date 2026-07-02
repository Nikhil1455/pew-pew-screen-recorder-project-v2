using System.Diagnostics;
using System.IO;
using System.Text;
using PewPewRecorder.Models;

namespace PewPewRecorder.Services;

public sealed class FfmpegRecorder : IDisposable
{
    private Process? _process;
    private AudioPipeCapture? _audioCapture;
    private bool _disposed;
    private string? _lastFfmpegError;

    public bool IsRecording => _process is { HasExited: false };

    public event EventHandler<string>? StatusChanged;
    public event EventHandler<int>? Exited;

    public async Task<string> StartAsync(RecordingSettings settings, VideoEncoder encoder, CancellationToken cancellationToken = default)
    {
        if (IsRecording)
            throw new InvalidOperationException("Recording is already in progress.");

        _lastFfmpegError = null;
        Directory.CreateDirectory(settings.OutputDirectory);

        var outputPath = Path.Combine(
            settings.OutputDirectory,
            $"Recording_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

        _audioCapture = new AudioPipeCapture();
        _audioCapture.Start(settings.SystemAudioDeviceId, settings.MicrophoneDeviceId);

        await Task.Delay(300, cancellationToken);

        var args = BuildArguments(settings, encoder, outputPath);
        RaiseStatus($"Starting → {Path.GetFileName(outputPath)}");

        var psi = new ProcessStartInfo
        {
            FileName = FfmpegLocator.GetFfmpegPath(),
            Arguments = args,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start FFmpeg.");

        _process = process;
        _ = Task.Run(() => DrainStderrAsync(process), cancellationToken);

        process.EnableRaisingEvents = true;
        process.Exited += (_, _) =>
        {
            var code = process.ExitCode;
            RaiseStatus(code == 0 ? "Recording saved." : $"FFmpeg exited with code {code}.");
            Exited?.Invoke(this, code);
            if (_process == process)
                _process = null;
        };

        await Task.Delay(800, cancellationToken);

        if (process.HasExited)
        {
            _audioCapture.Stop();
            _audioCapture = null;
            var detail = string.IsNullOrWhiteSpace(_lastFfmpegError)
                ? "FFmpeg failed to start recording."
                : _lastFfmpegError;
            throw new InvalidOperationException(detail);
        }

        RaiseStatus("Recording…");
        return outputPath;
    }

    public async Task StopAsync()
    {
        var process = _process;
        if (process is null || process.HasExited)
        {
            _audioCapture?.Stop();
            _audioCapture = null;
            return;
        }

        RaiseStatus("Stopping…");

        try
        {
            await process.StandardInput.WriteAsync('q');
            await process.StandardInput.FlushAsync();
        }
        catch
        {
            // Process may have already exited.
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
        }

        _audioCapture?.Stop();
        _audioCapture = null;
    }

    private static string BuildArguments(RecordingSettings settings, VideoEncoder encoder, string outputPath)
    {
        var pipePath = $@"\\.\pipe\{AudioPipeCapture.PipeName}";
        var sb = new StringBuilder();

        sb.Append("-hide_banner -loglevel warning -y ");
        sb.Append($"-f gdigrab -framerate {settings.Fps} -draw_mouse 1 -i desktop ");
        sb.Append($"-f s16le -ar {AudioPipeCapture.SampleRate} -ac {AudioPipeCapture.Channels} -i \"{pipePath}\" ");
        sb.Append("-map 0:v -map 1:a ");

        if (settings.Width != 0 && settings.Height != 0)
            sb.Append($"-vf scale={settings.Width}:{settings.Height} ");

        if (encoder.Name == "h264_qsv")
            sb.Append("-init_hw_device qsv=hw -filter_hw_device hw ");

        sb.Append($"-c:v {encoder.Name} ");
        foreach (var arg in encoder.Args)
            sb.Append(arg).Append(' ');
        sb.Append("-c:a aac -b:a 192k -pix_fmt yuv420p ");
        sb.Append($"\"{outputPath}\"");

        return sb.ToString();
    }

    private async Task DrainStderrAsync(Process process)
    {
        try
        {
            while (!process.StandardError.EndOfStream)
            {
                var line = await process.StandardError.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                _lastFfmpegError = line.Trim();
                RaiseStatus(_lastFfmpegError);
            }
        }
        catch
        {
            // Ignore read errors on shutdown.
        }
    }

    private void RaiseStatus(string message) => StatusChanged?.Invoke(this, message);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _audioCapture?.Dispose();

        if (_process is { HasExited: false })
            _process.Kill(entireProcessTree: true);

        _process?.Dispose();
    }
}

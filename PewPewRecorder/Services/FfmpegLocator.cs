using System.IO;

namespace PewPewRecorder.Services;

public static class FfmpegLocator
{
    public static string GetAppDirectory()
    {
        var exePath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(exePath))
            return Path.GetDirectoryName(exePath) ?? AppContext.BaseDirectory;
        return AppContext.BaseDirectory;
    }

    public static string GetFfmpegPath()
    {
        var baseDir = GetAppDirectory();
        var candidates = new[]
        {
            Path.Combine(baseDir, "ffmpeg", "ffmpeg.exe"),
            Path.Combine(baseDir, "ffmpeg.exe"),
            Path.Combine(AppContext.BaseDirectory, "ffmpeg", "ffmpeg.exe"),
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
                return path;
        }

        return "ffmpeg";
    }

    public static bool IsAvailable()
    {
        var path = GetFfmpegPath();
        return path != "ffmpeg" && File.Exists(path);
    }
}

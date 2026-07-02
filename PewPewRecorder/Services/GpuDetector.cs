using System.Management;

namespace PewPewRecorder.Services;

public static class GpuDetector
{
    public static string DetectVendor()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            var names = searcher.Get()
                .Cast<ManagementObject>()
                .Select(o => o["Name"]?.ToString() ?? "")
                .Where(n => n.Length > 0)
                .ToList();

            if (names.Any(n => n.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)))
                return "nvidia";
            if (names.Any(n => n.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                                n.Contains("Radeon", StringComparison.OrdinalIgnoreCase)))
                return "amd";
            if (names.Any(n => n.Contains("Intel", StringComparison.OrdinalIgnoreCase)))
                return "intel";
        }
        catch
        {
            // WMI unavailable — encoder probe will decide.
        }

        return "unknown";
    }
}

using System.Security.Cryptography;
using System.Text;
using MotionStudio.Application.Contracts;

namespace MotionStudio.Desktop;

public sealed class ElectronDesktopHostBridge : IDesktopHostBridge
{
    private readonly string? _expectedToken;

    public ElectronDesktopHostBridge(string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(contentRootPath))
        {
            throw new ArgumentException("Content root path cannot be empty.", nameof(contentRootPath));
        }

        string modeEnv = Environment.GetEnvironmentVariable("MOTIONSTUDIO_DESKTOP_MODE") ?? string.Empty;
        IsDesktopMode = string.Equals(modeEnv, "electron", StringComparison.OrdinalIgnoreCase);
        _expectedToken = Environment.GetEnvironmentVariable("MOTIONSTUDIO_DESKTOP_TOKEN");

        string? configuredProjectsDir = Environment.GetEnvironmentVariable("MOTIONSTUDIO_PROJECTS_DIR");
        if (!string.IsNullOrWhiteSpace(configuredProjectsDir))
        {
            SafeProjectsDirectory = Path.GetFullPath(configuredProjectsDir);
        }
        else
        {
            string candidateExamples = Path.GetFullPath(Path.Combine(contentRootPath, "..", "..", "examples"));
            SafeProjectsDirectory = Directory.Exists(candidateExamples)
                ? candidateExamples
                : Path.GetFullPath(Path.Combine(contentRootPath, "Projects"));
        }

        Directory.CreateDirectory(SafeProjectsDirectory);
    }

    public bool IsDesktopMode { get; }

    public string HostModeLabel => IsDesktopMode ? "Desktop (Electron 33 + Loopback .NET 10)" : "Web Browser (.NET 10 Interactive Server)";

    public string SafeProjectsDirectory { get; }

    public bool ValidateDesktopToken(string? providedToken)
    {
        if (!IsDesktopMode || string.IsNullOrWhiteSpace(_expectedToken))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(providedToken))
        {
            return false;
        }

        byte[] expectedBytes = Encoding.UTF8.GetBytes(_expectedToken);
        byte[] providedBytes = Encoding.UTF8.GetBytes(providedToken);
        return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }
}

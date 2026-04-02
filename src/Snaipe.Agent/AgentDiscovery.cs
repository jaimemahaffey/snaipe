using System.Text.Json;

namespace Snaipe.Agent;

/// <summary>
/// Manages the discovery file that advertises this agent to inspectors.
/// Creates a JSON file in a well-known temp directory on startup and removes it on dispose.
/// </summary>
public sealed class AgentDiscovery : IDisposable
{
    private readonly string _filePath;

    private AgentDiscovery(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>
    /// Create and write the discovery file.
    /// </summary>
    public static AgentDiscovery Create(string pipeName, string windowTitle)
    {
        var directory = GetDiscoveryDirectory();
        Console.WriteLine($"[Snaipe.Agent] Discovery directory: {directory}");
        Directory.CreateDirectory(directory);

        // On Linux, restrict directory permissions to owner only.
        if (!OperatingSystem.IsWindows())
        {
            try { File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); }
            catch { /* Best effort — may fail on some filesystems. */ }
        }

        var pid = Environment.ProcessId;
        var filePath = Path.Combine(directory, $"{pid}.json");

        var discovery = new
        {
            pid,
            processName = Environment.ProcessPath is { } p
                ? Path.GetFileNameWithoutExtension(p)
                : "Unknown",
            windowTitle,
            pipeName,
            eventsPipeName = $"{pipeName}-events",
            protocolVersion = Protocol.MessageFraming.ProtocolVersion,
            agentVersion = "0.1.0",
            startedAt = DateTime.UtcNow.ToString("O"),
        };

        var json = JsonSerializer.Serialize(discovery, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);

        Console.WriteLine($"[Snaipe.Agent] Discovery file written: {filePath}");

        return new AgentDiscovery(filePath);
    }

    /// <summary>
    /// Get the directory where discovery files are stored.
    /// </summary>
    public static string GetDiscoveryDirectory()
    {
        return Path.Combine(Path.GetTempPath(), "snaipe");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
                System.Diagnostics.Debug.WriteLine($"[Snaipe.Agent] Discovery file removed: {_filePath}");
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}

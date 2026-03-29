using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Snaipe.Inspector;

/// <summary>
/// Scans the discovery directory for agent advertisement files and returns info about running agents.
/// </summary>
public static class AgentDiscoveryScanner
{
    /// <summary>
    /// Scan for available agents. Removes stale files for dead processes.
    /// </summary>
    public static List<AgentInfo> Scan()
    {
        var directory = GetDiscoveryDirectory();
        Console.WriteLine($"[Snaipe.Inspector] Scanning for agents in: {directory}");

        if (!Directory.Exists(directory))
        {
            Console.WriteLine("[Snaipe.Inspector] Discovery directory does not exist.");
            return [];
        }

        var files = Directory.GetFiles(directory, "*.json");
        Console.WriteLine($"[Snaipe.Inspector] Found {files.Length} discovery file(s).");
        var agents = new List<AgentInfo>();

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var pid = root.GetProperty("pid").GetInt32();

                // Verify the process is still running.
                try
                {
                    Process.GetProcessById(pid);
                }
                catch (ArgumentException)
                {
                    // Process no longer exists — clean up stale file.
                    try { File.Delete(file); } catch { }
                    continue;
                }

                var processName = root.GetProperty("processName").GetString() ?? "Unknown";
                var windowTitle = root.GetProperty("windowTitle").GetString() ?? "";
                var pipeName = root.GetProperty("pipeName").GetString() ?? "";
                var protocolVersion = root.GetProperty("protocolVersion").GetString() ?? "0.0";
                var agentVersion = root.TryGetProperty("agentVersion", out var av) ? av.GetString() : null;

                agents.Add(new AgentInfo
                {
                    Pid = pid,
                    ProcessName = processName,
                    WindowTitle = windowTitle,
                    PipeName = pipeName,
                    ProtocolVersion = protocolVersion,
                    AgentVersion = agentVersion,
                    FilePath = file,
                });
            }
            catch
            {
                // Skip malformed files.
            }
        }

        return agents;
    }

    /// <summary>
    /// Check if the agent's protocol version is compatible with the inspector.
    /// Only the major version must match.
    /// </summary>
    public static bool IsCompatible(string agentVersion)
    {
        var agentMajor = agentVersion.Split('.')[0];
        var inspectorMajor = Protocol.MessageFraming.ProtocolVersion.Split('.')[0];
        return agentMajor == inspectorMajor;
    }

    private static string GetDiscoveryDirectory()
    {
        return Path.Combine(Path.GetTempPath(), "snaipe");
    }
}

public class AgentInfo
{
    public int Pid { get; init; }
    public required string ProcessName { get; init; }
    public required string WindowTitle { get; init; }
    public required string PipeName { get; init; }
    public required string ProtocolVersion { get; init; }
    public string? AgentVersion { get; init; }
    public string? FilePath { get; init; }

    /// <summary>
    /// Display string for the agent dropdown: "ProcessName (PID pid) — WindowTitle"
    /// </summary>
    public string DisplayName =>
        string.IsNullOrEmpty(WindowTitle)
            ? $"{ProcessName} (PID {Pid})"
            : $"{ProcessName} (PID {Pid}) — {WindowTitle}";
}

using Microsoft.UI.Xaml;
using Snaipe.Protocol;

namespace Snaipe.Agent;

/// <summary>
/// Entry point for the in-process agent. Call <see cref="Attach"/> from the target app
/// to start serving visual tree data to an inspector.
/// </summary>
public sealed class SnaipeAgent : IDisposable
{
    private readonly Window _window;
    private CancellationTokenSource? _cts;

    private SnaipeAgent(Window window)
    {
        _window = window;
    }

    /// <summary>
    /// Attach the agent to a running Uno window.
    /// </summary>
    public static SnaipeAgent Attach(Window window)
    {
        var agent = new SnaipeAgent(window);
        agent.Start();
        return agent;
    }

    private void Start()
    {
        _cts = new CancellationTokenSource();
        // TODO: start IPC listener (named pipe or TCP)
        // For now, just prove we can walk the tree
    }

    /// <summary>
    /// Snapshot the current visual tree.
    /// </summary>
    public ElementNode? GetTree()
    {
        if (_window.Content is UIElement root)
            return VisualTreeWalker.BuildTree(root);

        return null;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}

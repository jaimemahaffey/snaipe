using System.IO.Pipes;
using Snaipe.Agent;
using Snaipe.Protocol;
using Xunit;

namespace Snaipe.Agent.Tests;

public class AgentEventServerTests
{
    [Fact]
    public async Task EnqueueEvent_DeliversMessageToConnectedClient()
    {
        var pipeName = $"snaipe-test-{Guid.NewGuid():N}";
        var server = new AgentEventServer(pipeName);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        _ = Task.Run(() => server.RunAsync(cts.Token));

        // Give server time to start listening
        await Task.Delay(150);

        await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.In, PipeOptions.Asynchronous);
        await client.ConnectAsync(cts.Token);

        server.EnqueueEvent(new ElementUnderCursorEvent
        {
            MessageId = "test-1",
            ElementId = "elem-abc",
            TypeName = "Button"
        });

        var received = await MessageFraming.ReadMessageAsync(client, cts.Token);
        cts.Cancel();

        var typed = Assert.IsType<ElementUnderCursorEvent>(received);
        Assert.Equal("elem-abc", typed.ElementId);
        Assert.Equal("Button", typed.TypeName);

        server.Dispose();
    }

    [Fact]
    public async Task EnqueueEvent_SilentlyDropsWhenNoClientConnected()
    {
        var pipeName = $"snaipe-test-{Guid.NewGuid():N}";
        var server = new AgentEventServer(pipeName);

        // Enqueue without starting RunAsync (no client)
        server.EnqueueEvent(new PickModeActiveEvent { MessageId = "test-2", Active = true });

        // No exception — event was silently dropped (TryWrite returns false)
        server.Dispose();
        await Task.CompletedTask;
    }
}

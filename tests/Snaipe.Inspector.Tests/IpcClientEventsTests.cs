using System.IO.Pipes;
using Snaipe.Inspector;
using Snaipe.Protocol;
using Xunit;

namespace Snaipe.Inspector.Tests;

public class IpcClientEventsTests
{
    [Fact]
    public async Task ConnectEventsAsync_RaisesEventReceived_WhenServerPushesMessage()
    {
        var pipeName = $"snaipe-evttest-{Guid.NewGuid():N}";
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Create a server pipe that will push one event.
        await using var serverPipe = new NamedPipeServerStream(
            pipeName, PipeDirection.Out, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        var client = new InspectorIpcClient();
        InspectorMessage? received = null;
        client.EventReceived += msg => received = msg;

        // Connect client and server concurrently.
        var serverTask = serverPipe.WaitForConnectionAsync(cts.Token);
        await client.ConnectEventsAsync(pipeName, cts.Token);
        await serverTask;

        // Push one event from the server side.
        var pushed = new PickModeActiveEvent { MessageId = "test-evt-1", Active = true };
        await MessageFraming.WriteMessageAsync(serverPipe, pushed, cts.Token);

        // Give the read loop time to process.
        await Task.Delay(200, cts.Token);
        cts.Cancel();

        var typed = Assert.IsType<PickModeActiveEvent>(received);
        Assert.True(typed.Active);
        Assert.Equal("test-evt-1", typed.MessageId);

        client.Dispose();
    }

    [Fact]
    public async Task ConnectEventsAsync_DisconnectCleansUp_NoException()
    {
        var pipeName = $"snaipe-evttest2-{Guid.NewGuid():N}";
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await using var serverPipe = new NamedPipeServerStream(
            pipeName, PipeDirection.Out, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        var client = new InspectorIpcClient();
        client.EventReceived += _ => { };

        var serverTask = serverPipe.WaitForConnectionAsync(cts.Token);
        await client.ConnectEventsAsync(pipeName, cts.Token);
        await serverTask;

        // Disconnect should not throw.
        client.Disconnect();
        client.Dispose();
        cts.Cancel();
    }
}

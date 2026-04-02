using System.IO;
using Snaipe.Protocol;
using Xunit;

namespace Snaipe.Agent.Tests;

public class ProtocolRoundTripTests
{
    [Fact]
    public async Task ElementUnderCursorEvent_RoundTrips()
    {
        var original = new ElementUnderCursorEvent
        {
            MessageId = "msg-1",
            ElementId = "elem-abc",
            TypeName = "Button"
        };

        var stream = new MemoryStream();
        await MessageFraming.WriteMessageAsync(stream, original);
        stream.Position = 0;

        var result = await MessageFraming.ReadMessageAsync(stream);

        var typed = Assert.IsType<ElementUnderCursorEvent>(result);
        Assert.Equal("msg-1", typed.MessageId);
        Assert.Equal("elem-abc", typed.ElementId);
        Assert.Equal("Button", typed.TypeName);
    }

    [Fact]
    public async Task PickModeActiveEvent_RoundTrips()
    {
        var original = new PickModeActiveEvent
        {
            MessageId = "msg-2",
            Active = true
        };

        var stream = new MemoryStream();
        await MessageFraming.WriteMessageAsync(stream, original);
        stream.Position = 0;

        var result = await MessageFraming.ReadMessageAsync(stream);

        var typed = Assert.IsType<PickModeActiveEvent>(result);
        Assert.Equal("msg-2", typed.MessageId);
        Assert.True(typed.Active);
    }

    [Fact]
    public async Task TreeResponse_WithMultipleRoots_RoundTrips()
    {
        var original = new TreeResponse
        {
            MessageId = "msg-3",
            Roots =
            [
                new ElementNode { Id = "root-1", TypeName = "Grid" },
                new ElementNode { Id = "popup-1", TypeName = "[Popup]" }
            ]
        };

        var stream = new MemoryStream();
        await MessageFraming.WriteMessageAsync(stream, original);
        stream.Position = 0;

        var result = await MessageFraming.ReadMessageAsync(stream);

        var typed = Assert.IsType<TreeResponse>(result);
        Assert.Equal(2, typed.Roots.Count);
        Assert.Equal("root-1", typed.Roots[0].Id);
        Assert.Equal("popup-1", typed.Roots[1].Id);
    }
}

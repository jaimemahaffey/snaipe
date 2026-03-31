using Snaipe.Agent;
using Snaipe.Protocol;
using Xunit;

namespace Snaipe.Agent.Tests;

public class ObjectPropertyWriterTests
{
    private class Settings
    {
        public string Title { get; set; } = "";
        public int Count { get; set; }
        public bool Enabled { get; set; }
        public string ReadOnly => "fixed";
    }

    [Fact]
    public void SetProperty_WritesStringValue()
    {
        var s = new Settings();
        var result = ObjectPropertyWriter.SetProperty(s, "Title", "Hello");
        Assert.True(result.Success);
        Assert.Equal("Hello", s.Title);
    }

    [Fact]
    public void SetProperty_WritesIntValue()
    {
        var s = new Settings();
        var result = ObjectPropertyWriter.SetProperty(s, "Count", "42");
        Assert.True(result.Success);
        Assert.Equal(42, s.Count);
    }

    [Fact]
    public void SetProperty_WritesBoolValue()
    {
        var s = new Settings();
        var result = ObjectPropertyWriter.SetProperty(s, "Enabled", "True");
        Assert.True(result.Success);
        Assert.True(s.Enabled);
    }

    [Fact]
    public void SetProperty_UnknownProperty_ReturnsPropertyNotFoundError()
    {
        var s = new Settings();
        var result = ObjectPropertyWriter.SetProperty(s, "DoesNotExist", "x");
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PropertyNotFound, result.ErrorCode);
    }

    [Fact]
    public void SetProperty_ReadOnlyProperty_ReturnsPropertyReadOnlyError()
    {
        var s = new Settings();
        var result = ObjectPropertyWriter.SetProperty(s, "ReadOnly", "x");
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.PropertyReadOnly, result.ErrorCode);
    }

    [Fact]
    public void SetProperty_InvalidValue_ReturnsInvalidPropertyValueError()
    {
        var s = new Settings();
        var result = ObjectPropertyWriter.SetProperty(s, "Count", "not-a-number");
        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.InvalidPropertyValue, result.ErrorCode);
    }
}

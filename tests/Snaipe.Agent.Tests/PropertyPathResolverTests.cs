using Snaipe.Agent;
using Snaipe.Protocol;
using Xunit;

namespace Snaipe.Agent.Tests;

public class PropertyPathResolverTests
{
    // Test classes for CLR traversal
    private class Address { public string City { get; set; } = ""; }
    private class Person
    {
        public string Name { get; set; } = "";
        public Address? Address { get; set; }
    }
    private class Root { public Person? Owner { get; set; } }

    [Fact]
    public void TraversePath_EmptySegments_ReturnsRoot()
    {
        var root = new Root { Owner = new Person { Name = "Alice" } };
        var (value, errorCode, _) = PropertyPathResolver.TraversePath(root, [], 0);
        Assert.Equal(root, value);
        Assert.Equal(0, errorCode);
    }

    [Fact]
    public void TraversePath_SingleSegment_ReturnsPropertyValue()
    {
        var person = new Person { Name = "Alice" };
        var root = new Root { Owner = person };
        var (value, errorCode, _) = PropertyPathResolver.TraversePath(root, ["Owner"], 0);
        Assert.Equal(person, value);
        Assert.Equal(0, errorCode);
    }

    [Fact]
    public void TraversePath_MultiSegment_TraversesChain()
    {
        var addr = new Address { City = "Paris" };
        var person = new Person { Address = addr };
        var root = new Root { Owner = person };
        var (value, errorCode, _) = PropertyPathResolver.TraversePath(root, ["Owner", "Address"], 0);
        Assert.Equal(addr, value);
        Assert.Equal(0, errorCode);
    }

    [Fact]
    public void TraversePath_NullSegment_ReturnsElementNotFoundError()
    {
        var root = new Root { Owner = null };  // Owner is null
        var (value, errorCode, _) = PropertyPathResolver.TraversePath(root, ["Owner", "Address"], 0);
        Assert.Null(value);
        Assert.Equal(ErrorCodes.ElementNotFound, errorCode);
    }

    [Fact]
    public void TraversePath_MissingProperty_ReturnsElementNotFoundError()
    {
        var root = new Root();
        var (value, errorCode, _) = PropertyPathResolver.TraversePath(root, ["DoesNotExist"], 0);
        Assert.Null(value);
        Assert.Equal(ErrorCodes.ElementNotFound, errorCode);
    }

    [Fact]
    public void TraversePath_StartIndex_SkipsEarlierSegments()
    {
        // startIndex=1 skips "Owner", starts at "Address"
        var addr = new Address { City = "Paris" };
        var person = new Person { Address = addr };
        // Pass person as root (already resolved), skip 0 segments
        var (value, errorCode, _) = PropertyPathResolver.TraversePath(person, ["Address"], 0);
        Assert.Equal(addr, value);
        Assert.Equal(0, errorCode);
    }
}

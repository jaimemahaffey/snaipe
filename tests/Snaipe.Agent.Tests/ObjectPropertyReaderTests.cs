using Snaipe.Agent;
using Xunit;

namespace Snaipe.Agent.Tests;

public class ObjectPropertyReaderTests
{
    private class Person
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public bool IsActive { get; set; }
        public Person? Child { get; set; }
        public string? NullProp { get; set; }
        public string ReadOnlyProp => "const";
        public string ThrowingProp => throw new InvalidOperationException("boom");
    }

    [Fact]
    public void GetProperties_ReturnsAllPublicReadableProperties()
    {
        var person = new Person { Name = "Alice", Age = 30 };
        var props = ObjectPropertyReader.GetProperties(person);
        var names = props.Select(p => p.Name).ToList();
        Assert.Contains("Name", names);
        Assert.Contains("Age", names);
        Assert.Contains("IsActive", names);
    }

    [Fact]
    public void GetProperties_AllHaveCategoryProperties()
    {
        var props = ObjectPropertyReader.GetProperties(new Person());
        Assert.All(props, p => Assert.Equal("Properties", p.Category));
    }

    [Fact]
    public void GetProperties_ObjectValuedNonNull_SetsIsObjectValued()
    {
        var person = new Person { Child = new Person { Name = "Bob" } };
        var props = ObjectPropertyReader.GetProperties(person);
        var childProp = props.First(p => p.Name == "Child");
        Assert.True(childProp.IsObjectValued);
        Assert.Equal("Object", childProp.ValueKind);
    }

    [Fact]
    public void GetProperties_ObjectValuedNull_IsObjectValuedFalse()
    {
        var person = new Person { Child = null };
        var props = ObjectPropertyReader.GetProperties(person);
        var childProp = props.First(p => p.Name == "Child");
        Assert.False(childProp.IsObjectValued);
    }

    [Fact]
    public void GetProperties_ReadOnlyProperty_IsReadOnlyTrue()
    {
        var props = ObjectPropertyReader.GetProperties(new Person());
        var ro = props.First(p => p.Name == "ReadOnlyProp");
        Assert.True(ro.IsReadOnly);
    }

    [Fact]
    public void GetProperties_SkipsThrowingProperties()
    {
        // Should not throw; ThrowingProp silently absent
        var props = ObjectPropertyReader.GetProperties(new Person());
        Assert.DoesNotContain(props, p => p.Name == "ThrowingProp");
    }
}

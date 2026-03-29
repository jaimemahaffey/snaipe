namespace Snaipe.Agent;

/// <summary>
/// Thin wrapper so we can generate stable-ish IDs for DependencyObjects.
/// </summary>
internal static class RuntimeHelpers
{
    public static int GetHashCode(object obj) =>
        System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}

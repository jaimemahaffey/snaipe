namespace Snaipe.Inspector.ViewModels;

/// <summary>
/// One entry in the property grid's navigation breadcrumb trail.
/// <see cref="Path"/> is the full PropertyPath needed to reach this level from the element root.
/// </summary>
public record BreadcrumbSegment(string Label, string[] Path);

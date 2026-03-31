namespace Snaipe.Inspector.ViewModels;

/// <summary>
/// One entry in the property grid's navigation breadcrumb trail.
/// <see cref="Path"/> is the full PropertyPath needed to reach this level from the element root.
/// <see cref="NavigateCommand"/> is wired at creation time to call NavigateToBreadcrumbCommand.
/// </summary>
public record BreadcrumbSegment(string Label, string[] Path, RelayCommand? NavigateCommand = null);

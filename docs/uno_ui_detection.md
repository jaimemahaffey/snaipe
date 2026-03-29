# Snaipe Inspector Capabilities Research

This document outlines the technical feasibility and implementation strategies for detecting various XAML and UI framework features in the context of the **Uno Platform** and **WinUI 3** (`Microsoft.UI.Xaml`), which Snaipe targets.

## 1. Detecting Triggers Being Applied

Unlike WPF, which heavily relies on `Style.Triggers` and `DataTrigger`s, Uno Platform and WinUI primarily manage visual states using the `VisualStateManager` (VSM). 

To detect which "trigger" or visual state is active:
* **VisualStateGroups**: You can retrieve the visual states attached to an element (usually the root element of a `ControlTemplate`) by calling `VisualStateManager.GetVisualStateGroups(element)`. This returns an `IList<VisualStateGroup>`.
* **CurrentState Property**: Each `VisualStateGroup` exposes a `CurrentState` property. When a control transitions states (e.g., from "Normal" to "PointerOver"), `CurrentState` updates to reflect the active `VisualState`. 
* **StateTriggers**: Within a `VisualState`, developers can use `StateTriggers` (like `AdaptiveTrigger`). While there isn't always a public API to evaluate a trigger's boolean condition directly, its active status is indirectly observable through whether its parent `VisualState` matches the `CurrentState` of the group.

*Implementation in Snaipe:* The agent can read `VisualStateManager.GetVisualStateGroups` on tree nodes. If groups exist, it can enumerate them and extract `group.CurrentState?.Name` to display the active states in the Property Grid under a dedicated "Visual States" category.

## 2. Style Inheritance

Styles in XAML support inheritance via the `BasedOn` property. Detangling the active style hierarchy is straightforward:
* **Explicit & Implicit Styles**: You can access the currently applied style via `FrameworkElement.Style`.
* **BasedOn Traversal**: The `Style` class has a `BasedOn` property that returns the parent `Style`. Snaipe can recursively walk this chain until `BasedOn` is null to construct the full inheritance tree.
* **Default Styles**: If `FrameworkElement.Style` is `null`, the control is likely using its default theme style defined in `generic.xaml`. You can infer the default style lookup by reading the control's `DefaultStyleKey` property (typically its type).

*Implementation in Snaipe:* Add a "Style Hierarchy" view in the Property Grid that surfaces `element.Style` and recursively follows `Style.BasedOn`.

## 3. DataTemplates Loaded

Identifying which `DataTemplate` or `ControlTemplate` is materialized involves both reading property values and observing the visual tree:
* **Template Properties**: You can inspect `ContentPresenter.ContentTemplate`, `ItemsControl.ItemTemplate`, or `Control.Template` via the `PropertyReader` to see the assigned template.
* **Visual Tree instantiation**: When a `DataTemplate` loads, `VisualTreeHelper` traverses its materialized visual children. 
* **Identifying the Root**: On a `ContentPresenter` in WinUI/Uno, the framework stamps out the template. By walking the visual tree from the presenter, the immediate visual child is the root of the materialized `DataTemplate`. This allows Snaipe to visually link a sub-tree directly back to the active template.

## 4. DataContext in Use

The `DataContext` property flows down the visual tree via dependency property value inheritance. Snaipe can determine the source of a `DataContext` value to distinguish between inherited, localized, or bound contexts:
* **Reading the Value**: `element.GetValue(FrameworkElement.DataContextProperty)` returns the effective `DataContext`.
* **Determining the Source**: 
  * Call `element.ReadLocalValue(FrameworkElement.DataContextProperty)`.
  * If it returns `DependencyProperty.UnsetValue`, the `DataContext` is **inherited** from a visual parent.
  * If the element has a binding on the `DataContext` itself, calling `element.GetBindingExpression(FrameworkElement.DataContextProperty)` will return a `BindingExpression`, indicating the context is **data-bound**.
  * Otherwise, if `ReadLocalValue` returns an object, the context was set **locally** (e.g., in XAML or Code-Behind).

*Implementation in Snaipe:* The Property Grid can format the `DataContext` property with an indicator (e.g., `[Inherited]`, `[Local]`, or `[Bound]`) based on the result of `ReadLocalValue` and `GetBindingExpression`.

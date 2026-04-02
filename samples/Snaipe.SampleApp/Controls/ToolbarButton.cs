// samples/Snaipe.SampleApp/Controls/ToolbarButton.cs
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace Snaipe.SampleApp.Controls;

public sealed class ToolbarButton : Control
{
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(string), typeof(ToolbarButton),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(ToolbarButton),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(ToolbarButton),
            new PropertyMetadata(false, OnIsActiveChanged));

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(nameof(Command), typeof(System.Windows.Input.ICommand), typeof(ToolbarButton),
            new PropertyMetadata(null));

    public string Icon  { get => (string)GetValue(IconProperty);  set => SetValue(IconProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public bool IsActive { get => (bool)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
    public System.Windows.Input.ICommand? Command { get => (System.Windows.Input.ICommand?)GetValue(CommandProperty); set => SetValue(CommandProperty, value); }

    public ToolbarButton()
    {
        DefaultStyleKey = typeof(ToolbarButton);
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        VisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled", false);
        VisualStateManager.GoToState(this, IsActive ? "Active" : "Inactive", false);
    }

    protected override void OnPointerEntered(PointerRoutedEventArgs e)
    {
        base.OnPointerEntered(e);
        if (IsEnabled) VisualStateManager.GoToState(this, "PointerOver", true);
    }

    protected override void OnPointerExited(PointerRoutedEventArgs e)
    {
        base.OnPointerExited(e);
        VisualStateManager.GoToState(this, IsEnabled ? "Normal" : "Disabled", true);
    }

    protected override void OnPointerPressed(PointerRoutedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (IsEnabled)
        {
            VisualStateManager.GoToState(this, "Pressed", true);
            if (Command?.CanExecute(null) == true)
            {
                Command.Execute(null);
            }
        }
    }

    protected override void OnPointerReleased(PointerRoutedEventArgs e)
    {
        base.OnPointerReleased(e);
        VisualStateManager.GoToState(this, IsEnabled ? "PointerOver" : "Disabled", true);
    }

    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var btn = (ToolbarButton)d;
        VisualStateManager.GoToState(btn, (bool)e.NewValue ? "Active" : "Inactive", true);
    }
}

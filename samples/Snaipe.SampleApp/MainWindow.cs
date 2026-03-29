using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Snaipe.SampleApp;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        Title = "Snaipe Sample App";
        Content = BuildSampleUI();
    }

    private static UIElement BuildSampleUI()
    {
        var root = new Grid
        {
            Padding = new Thickness(16),
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
            },
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x2E, 0x34, 0x40)),
        };

        // Header
        var header = new TextBlock
        {
            Text = "Snaipe Sample App",
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x88, 0xC0, 0xD0)),
            Margin = new Thickness(0, 0, 0, 16),
            Name = "HeaderText",
        };
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        // Controls panel
        var controlsPanel = new StackPanel
        {
            Spacing = 8,
            Name = "ControlsPanel",
        };
        Grid.SetRow(controlsPanel, 1);

        var nameInput = new TextBox
        {
            PlaceholderText = "Enter your name...",
            Name = "NameInput",
        };
        controlsPanel.Children.Add(nameInput);

        var greetButton = new Button
        {
            Content = "Greet",
            Name = "GreetButton",
        };

        var greetOutput = new TextBlock
        {
            Text = "",
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xA3, 0xBE, 0x8C)),
            Name = "GreetOutput",
        };

        greetButton.Click += (_, _) =>
        {
            var name = nameInput.Text;
            greetOutput.Text = string.IsNullOrWhiteSpace(name)
                ? "Hello, World!"
                : $"Hello, {name}!";
        };

        controlsPanel.Children.Add(greetButton);
        controlsPanel.Children.Add(greetOutput);

        var checkBox = new CheckBox
        {
            Content = "Enable feature",
            Name = "FeatureCheckBox",
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xD8, 0xDE, 0xE9)),
        };
        controlsPanel.Children.Add(checkBox);

        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = 50,
            Name = "VolumeSlider",
        };
        controlsPanel.Children.Add(slider);

        var styledButtonStyle = new Style(typeof(Button));
        styledButtonStyle.Setters.Add(new Setter(Control.ForegroundProperty,
            new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xBF, 0x61, 0x6A))));
        styledButtonStyle.Setters.Add(new Setter(Control.FontSizeProperty, 18.0));
        var styledButton = new Button
        {
            Content = "Styled Button",
            Name = "StyledButton",
            Style = styledButtonStyle,
        };
        controlsPanel.Children.Add(styledButton);

        var dataContextPanel = new StackPanel
        {
            Name = "DataContextPanel",
            DataContext = new { UserName = "Alice", Role = "Admin" },
            Spacing = 4,
        };
        var dcLabel = new TextBlock
        {
            Text = "DataContext inherited below:",
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xEB, 0xCB, 0x8B)),
            Name = "DataContextLabel",
        };
        var dcChild = new TextBlock
        {
            Text = "I inherit DataContext from parent",
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xD8, 0xDE, 0xE9)),
            Name = "DataContextChild",
        };
        dataContextPanel.Children.Add(dcLabel);
        dataContextPanel.Children.Add(dcChild);
        controlsPanel.Children.Add(dataContextPanel);

        root.Children.Add(controlsPanel);

        var listView = new ListView
        {
            Name = "ItemsList",
        };
        listView.Items.Add("Item 1");
        listView.Items.Add("Item 2");
        listView.Items.Add("Item 3");
        listView.Items.Add("Item 4");
        listView.Items.Add("Item 5");
        Grid.SetRow(listView, 2);
        root.Children.Add(listView);

        return root;
    }
}

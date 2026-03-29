using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Snaipe.Protocol;

namespace Snaipe.Inspector;

/// <summary>
/// Main window for the Snaipe Inspector. Contains a connection bar,
/// tree view, property grid, and preview pane.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly InspectorIpcClient _client = new();
    private List<AgentInfo> _agents = [];
    private AgentInfo? _selectedAgent;
    private ElementNode? _currentTree;
    private string? _selectedElementId;

    private static readonly string LogFile = "/tmp/snaipe-inspector.log";

    private static void Log(string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {msg}";
        try { File.AppendAllText(LogFile, line + Environment.NewLine); } catch { }
    }

    // UI elements
    private ComboBox _agentDropdown = null!;
    private Button _connectButton = null!;
    private Button _refreshButton = null!;
    private TreeView _treeView = null!;
    private StackPanel _propertyPanel = null!;
    private TextBlock _statusText = null!;
    private TextBlock _previewText = null!;

    public MainWindow()
    {
        Log("MainWindow constructor started");
        Title = "Snaipe Inspector";
        BuildUI();
        Log("BuildUI completed");
        RefreshAgentList();
        Log("Initial RefreshAgentList completed");
    }

    private void BuildUI()
    {
        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },      // Connection bar
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }, // Main content
                new RowDefinition { Height = GridLength.Auto },      // Status bar
            },
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x2E, 0x34, 0x40)),
        };

        // --- Connection Bar ---
        var connectionBar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Padding = new Thickness(8),
            Spacing = 8,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x3B, 0x42, 0x52)),
        };

        _agentDropdown = new ComboBox
        {
            MinWidth = 300,
            PlaceholderText = "Select an agent...",
        };
        _agentDropdown.SelectionChanged += OnAgentSelected;

        var refreshAgentsButton = new Button { Content = "⟳" };
        refreshAgentsButton.Click += (_, _) =>
        {
            Log("Refresh button clicked!");
            RefreshAgentList();
        };

        _connectButton = new Button { Content = "Connect", IsEnabled = false };
        _connectButton.Click += OnConnectClick;

        _refreshButton = new Button { Content = "Refresh Tree", IsEnabled = false };
        _refreshButton.Click += OnRefreshTreeClick;

        connectionBar.Children.Add(_agentDropdown);
        connectionBar.Children.Add(refreshAgentsButton);
        connectionBar.Children.Add(_connectButton);
        connectionBar.Children.Add(_refreshButton);
        Grid.SetRow(connectionBar, 0);
        root.Children.Add(connectionBar);

        // --- Main Content ---
        var mainGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) },
            },
        };
        Grid.SetRow(mainGrid, 1);

        // Left pane: Tree View
        var treeScroll = new ScrollViewer
        {
            Padding = new Thickness(4),
        };
        _treeView = new TreeView
        {
            SelectionMode = TreeViewSelectionMode.Single,
        };
        _treeView.ItemInvoked += OnTreeItemInvoked;
        treeScroll.Content = _treeView;
        Grid.SetColumn(treeScroll, 0);
        mainGrid.Children.Add(treeScroll);

        // Right pane: Property Grid + Preview
        var rightPane = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(3, GridUnitType.Star) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
            },
        };
        Grid.SetColumn(rightPane, 1);

        var propertyScroll = new ScrollViewer { Padding = new Thickness(4) };
        _propertyPanel = new StackPanel { Spacing = 2 };
        propertyScroll.Content = _propertyPanel;
        Grid.SetRow(propertyScroll, 0);
        rightPane.Children.Add(propertyScroll);

        // Preview Pane
        var previewBorder = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x43, 0x4C, 0x5E)),
            Padding = new Thickness(8),
        };
        _previewText = new TextBlock
        {
            Text = "Preview pane — select an element to see details.",
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xD8, 0xDE, 0xE9)),
            TextWrapping = TextWrapping.Wrap,
        };
        previewBorder.Child = _previewText;
        Grid.SetRow(previewBorder, 1);
        rightPane.Children.Add(previewBorder);

        mainGrid.Children.Add(rightPane);
        root.Children.Add(mainGrid);

        // --- Status Bar ---
        var statusBar = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x3B, 0x42, 0x52)),
            Padding = new Thickness(8, 4, 8, 4),
        };
        _statusText = new TextBlock
        {
            Text = "Ready",
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xA3, 0xBE, 0x8C)),
            FontSize = 12,
        };
        statusBar.Child = _statusText;
        Grid.SetRow(statusBar, 2);
        root.Children.Add(statusBar);

        Content = root;
    }

    private void RefreshAgentList()
    {
        Log("RefreshAgentList called");
        try
        {
            _agents = AgentDiscoveryScanner.Scan();
            Log($"Found {_agents.Count} agent(s)");
            _agentDropdown.Items.Clear();

            if (_agents.Count == 0)
            {
                _agentDropdown.PlaceholderText = "No agents found";
                _connectButton.IsEnabled = false;
                SetStatus("No Snaipe agents detected. Ensure the target app calls SnaipeAgent.Attach().");
                return;
            }

            _agentDropdown.PlaceholderText = "Select an agent...";
            foreach (var agent in _agents)
            {
                Log($"  Agent: {agent.DisplayName} (compatible: {AgentDiscoveryScanner.IsCompatible(agent.ProtocolVersion)})");
                var item = new ComboBoxItem
                {
                    Content = agent.DisplayName,
                    Tag = agent,
                    IsEnabled = AgentDiscoveryScanner.IsCompatible(agent.ProtocolVersion),
                };
                _agentDropdown.Items.Add(item);
            }
        }
        catch (Exception ex)
        {
            Log($"RefreshAgentList ERROR: {ex}");
        }
    }

    private void OnAgentSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_agentDropdown.SelectedItem is ComboBoxItem { Tag: AgentInfo agent })
        {
            _selectedAgent = agent;
            _connectButton.IsEnabled = true;
        }
    }

    private async void OnConnectClick(object sender, RoutedEventArgs e)
    {
        if (_client.IsConnected)
        {
            _client.Disconnect();
            _connectButton.Content = "Connect";
            _refreshButton.IsEnabled = false;
            _treeView.RootNodes.Clear();
            _propertyPanel.Children.Clear();
            SetStatus("Disconnected.");
            return;
        }

        if (_selectedAgent is null) return;

        try
        {
            SetStatus($"Connecting to {_selectedAgent.DisplayName}...");
            await _client.ConnectAsync(_selectedAgent.PipeName);
            _connectButton.Content = "Disconnect";
            _refreshButton.IsEnabled = true;
            SetStatus($"Connected to {_selectedAgent.DisplayName}");
            await FetchAndDisplayTree();
        }
        catch (Exception ex)
        {
            SetStatus($"Connection failed: {ex.Message}");
        }
    }

    private async void OnRefreshTreeClick(object sender, RoutedEventArgs e)
    {
        await FetchAndDisplayTree();
    }

    private async Task FetchAndDisplayTree()
    {
        try
        {
            SetStatus("Fetching tree...");
            var response = await _client.SendAsync<TreeResponse>(
                new GetTreeRequest { MessageId = Guid.NewGuid().ToString("N") });

            _currentTree = response.Root;
            PopulateTreeView(response.Root);
            SetStatus($"Tree loaded ({CountNodes(response.Root)} elements).");
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
        }
    }

    private void PopulateTreeView(ElementNode root)
    {
        // Save expanded/selected state.
        var expandedIds = new HashSet<string>();
        CollectExpandedIds(_treeView.RootNodes, expandedIds);

        _treeView.RootNodes.Clear();
        var rootNode = CreateTreeNode(root);
        _treeView.RootNodes.Add(rootNode);

        // Restore expanded state.
        RestoreExpandedState(_treeView.RootNodes, expandedIds);
    }

    private TreeViewNode CreateTreeNode(ElementNode element)
    {
        var displayText = string.IsNullOrEmpty(element.Name)
            ? element.TypeName
            : $"{element.TypeName} \"{element.Name}\"";

        var node = new TreeViewNode
        {
            Content = displayText,
            IsExpanded = true,
        };

        // Store the element ID for later lookup.
        node.Content = new TreeNodeData(element.Id, displayText);

        foreach (var child in element.Children)
        {
            node.Children.Add(CreateTreeNode(child));
        }

        return node;
    }

    private async void OnTreeItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is TreeViewNode { Content: TreeNodeData data })
        {
            _selectedElementId = data.ElementId;
            await FetchAndDisplayProperties(data.ElementId);

            // Send highlight request.
            try
            {
                await _client.SendRawAsync(new HighlightElementRequest
                {
                    MessageId = Guid.NewGuid().ToString("N"),
                    ElementId = data.ElementId,
                    Show = true,
                });
            }
            catch { /* Best effort highlighting. */ }
        }
    }

    private async Task FetchAndDisplayProperties(string elementId)
    {
        try
        {
            var response = await _client.SendAsync<PropertiesResponse>(
                new GetPropertiesRequest
                {
                    MessageId = Guid.NewGuid().ToString("N"),
                    ElementId = elementId,
                });

            DisplayProperties(response.Properties);

            // Update preview.
            var element = FindElement(_currentTree, elementId);
            if (element is not null)
            {
                _previewText.Text = $"Type: {element.TypeName}\n" +
                    $"Name: {element.Name ?? "(none)"}\n" +
                    $"ID: {element.Id}\n" +
                    $"Children: {element.Children.Count}\n" +
                    $"Properties: {response.Properties.Count}";
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Error loading properties: {ex.Message}");
        }
    }

    private void DisplayProperties(List<PropertyEntry> properties)
    {
        _propertyPanel.Children.Clear();

        var groups = properties
            .GroupBy(p => p.Category)
            .OrderBy(g => g.Key switch
            {
                "Common" => 0,
                "Layout" => 1,
                "Appearance" => 2,
                "Data Context" => 3,
                "Visual States" => 4,
                "Style" => 5,
                "Template" => 6,
                _ => 7,
            });

        foreach (var group in groups)
        {
            // Category header — separator line + bold label.
            var headerBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x4C, 0x56, 0x6A)),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Margin = new Thickness(0, 12, 0, 0),
                Padding = new Thickness(0, 6, 0, 4),
                Child = new TextBlock
                {
                    Text = group.Key,
                    FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                    FontSize = 14,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x88, 0xC0, 0xD0)),
                },
            };
            _propertyPanel.Children.Add(headerBorder);

            foreach (var prop in group.OrderBy(p => p.Name))
            {
                // 4-column grid: Name (1.5*) | Type (0.7*) | Value (2*) | Binding (1*)
                var row = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) },
                        new ColumnDefinition { Width = new GridLength(0.7, GridUnitType.Star) },
                        new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) },
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    },
                    Padding = new Thickness(4, 2, 4, 2),
                };

                // Col 0: Property name.
                var nameBlock = new TextBlock
                {
                    Text = prop.Name,
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0xD8, 0xDE, 0xE9)),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetColumn(nameBlock, 0);
                row.Children.Add(nameBlock);

                // Col 1: ValueType in dimmed smaller text.
                var typeBlock = new TextBlock
                {
                    Text = prop.ValueType ?? "",
                    Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x61, 0x6E, 0x88)),
                    FontSize = 10,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                Grid.SetColumn(typeBlock, 1);
                row.Children.Add(typeBlock);

                // Col 2: Editor or read-only value display.
                UIElement valueElement;
                if (prop.IsReadOnly)
                {
                    valueElement = new TextBlock
                    {
                        Text = prop.Value ?? "(null)",
                        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x81, 0xA1, 0xC1)),
                        FontSize = 12,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    };
                }
                else
                {
                    valueElement = CreateEditor(prop);
                }
                Grid.SetColumn(valueElement, 2);
                row.Children.Add(valueElement);

                // Col 3: Binding expression in italic dimmed text (all properties).
                if (prop.BindingExpression is not null)
                {
                    var bindingBlock = new TextBlock
                    {
                        Text = prop.BindingExpression,
                        Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x61, 0x6E, 0x88)),
                        FontSize = 10,
                        FontStyle = Windows.UI.Text.FontStyle.Italic,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    };
                    Grid.SetColumn(bindingBlock, 3);
                    row.Children.Add(bindingBlock);
                }

                _propertyPanel.Children.Add(row);
            }
        }
    }

    private UIElement CreateEditor(PropertyEntry prop)
    {
        switch (prop.ValueKind)
        {
            case "Boolean":
            {
                var cb = new CheckBox
                {
                    IsChecked = bool.TryParse(prop.Value, out var v) && v,
                    Tag = prop.Name,
                };
                cb.Checked += async (_, _) => await SetPropertyValue(prop.Name, "True");
                cb.Unchecked += async (_, _) => await SetPropertyValue(prop.Name, "False");
                return cb;
            }

            case "Color":
            {
                var swatch = new Border
                {
                    Width = 16,
                    Height = 16,
                    BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x61, 0x6E, 0x88)),
                    BorderThickness = new Thickness(1),
                    Background = TryParseBrush(prop.Value),
                    VerticalAlignment = VerticalAlignment.Center,
                };

                var tb = new TextBox
                {
                    Text = prop.Value ?? "",
                    FontSize = 12,
                    Padding = new Thickness(4, 2, 4, 2),
                    Tag = prop.Name,
                    PlaceholderText = "#AARRGGBB",
                };
                tb.LostFocus += async (s, _) =>
                {
                    if (s is TextBox box && box.Tag is string name)
                    {
                        swatch.Background = TryParseBrush(box.Text);
                        await SetPropertyValue(name, box.Text);
                    }
                };

                var panel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 4,
                };
                panel.Children.Add(swatch);
                panel.Children.Add(tb);
                return panel;
            }

            case "Enum":
            {
                // EnumValues is not present on PropertyEntry — fall back to TextBox.
                var tb = new TextBox
                {
                    Text = prop.Value ?? "",
                    FontSize = 12,
                    Padding = new Thickness(4, 2, 4, 2),
                    Tag = prop.Name,
                };
                tb.LostFocus += async (s, _) =>
                {
                    if (s is TextBox box && box.Tag is string name)
                        await SetPropertyValue(name, box.Text);
                };
                return tb;
            }

            case "Thickness":
            {
                var tb = new TextBox
                {
                    Text = prop.Value ?? "",
                    FontSize = 12,
                    Padding = new Thickness(4, 2, 4, 2),
                    Tag = prop.Name,
                    PlaceholderText = "L,T,R,B",
                };
                tb.LostFocus += async (s, _) =>
                {
                    if (s is TextBox box && box.Tag is string name)
                        await SetPropertyValue(name, box.Text);
                };
                return tb;
            }

            default:
            {
                var tb = new TextBox
                {
                    Text = prop.Value ?? "",
                    FontSize = 12,
                    Padding = new Thickness(4, 2, 4, 2),
                    Tag = prop.Name,
                };
                tb.LostFocus += async (s, _) =>
                {
                    if (s is TextBox box && box.Tag is string name)
                        await SetPropertyValue(name, box.Text);
                };
                return tb;
            }
        }
    }

    /// <summary>
    /// Converts "#AARRGGBB" or "#RRGGBB" hex strings to a SolidColorBrush.
    /// Returns a gray fallback if parsing fails.
    /// </summary>
    private static SolidColorBrush TryParseBrush(string? hex)
    {
        if (!string.IsNullOrWhiteSpace(hex))
        {
            var s = hex.TrimStart('#');
            try
            {
                if (s.Length == 8)
                {
                    var a = Convert.ToByte(s[0..2], 16);
                    var r = Convert.ToByte(s[2..4], 16);
                    var g = Convert.ToByte(s[4..6], 16);
                    var b = Convert.ToByte(s[6..8], 16);
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(a, r, g, b));
                }
                else if (s.Length == 6)
                {
                    var r = Convert.ToByte(s[0..2], 16);
                    var g = Convert.ToByte(s[2..4], 16);
                    var b = Convert.ToByte(s[4..6], 16);
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, r, g, b));
                }
            }
            catch { /* fall through to gray fallback */ }
        }

        return new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x61, 0x6E, 0x88));
    }

    private async Task SetPropertyValue(string propertyName, string newValue)
    {
        if (_selectedElementId is null) return;

        try
        {
            var response = await _client.SendRawAsync(new SetPropertyRequest
            {
                MessageId = Guid.NewGuid().ToString("N"),
                ElementId = _selectedElementId,
                PropertyName = propertyName,
                NewValue = newValue,
            });

            if (response is PropertiesResponse pr)
            {
                DisplayProperties(pr.Properties);
                SetStatus($"Set {propertyName} = {newValue}");
            }
            else if (response is ErrorResponse err)
            {
                SetStatus($"Error: {err.Error}" + (err.Details is not null ? $" — {err.Details}" : ""));
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}");
        }
    }

    private void SetStatus(string text)
    {
        _statusText.Text = text;
    }

    private static int CountNodes(ElementNode node)
    {
        var count = 1;
        foreach (var child in node.Children)
            count += CountNodes(child);
        return count;
    }

    private static ElementNode? FindElement(ElementNode? root, string id)
    {
        if (root is null) return null;
        if (root.Id == id) return root;
        foreach (var child in root.Children)
        {
            var found = FindElement(child, id);
            if (found is not null) return found;
        }
        return null;
    }

    private static void CollectExpandedIds(IList<TreeViewNode> nodes, HashSet<string> ids)
    {
        foreach (var node in nodes)
        {
            if (node.IsExpanded && node.Content is TreeNodeData data)
                ids.Add(data.ElementId);
            CollectExpandedIds(node.Children, ids);
        }
    }

    private static void RestoreExpandedState(IList<TreeViewNode> nodes, HashSet<string> ids)
    {
        foreach (var node in nodes)
        {
            if (node.Content is TreeNodeData data && ids.Contains(data.ElementId))
                node.IsExpanded = true;
            RestoreExpandedState(node.Children, ids);
        }
    }
}

/// <summary>
/// Data stored in each TreeViewNode.Content to track element ID alongside display text.
/// </summary>
internal sealed record TreeNodeData(string ElementId, string DisplayText)
{
    public override string ToString() => DisplayText;
}

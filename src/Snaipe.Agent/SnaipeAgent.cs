using Microsoft.UI.Xaml;
using Snaipe.Protocol;

namespace Snaipe.Agent;

/// <summary>
/// Entry point for the in-process agent. Call <see cref="Attach"/> from the target app
/// to start serving visual tree data to an inspector.
/// </summary>
public sealed class SnaipeAgent : IDisposable
{
    private readonly Window _window;
    private readonly ElementTracker _tracker;
    private readonly HighlightOverlay _highlight;
    private readonly AgentEventServer _eventServer;
    private CancellationTokenSource? _cts;
    private AgentIpcServer? _ipcServer;
    private AgentDiscovery? _discovery;
    private PickModeManager? _pickMode;

    private SnaipeAgent(Window window)
    {
        _window = window;
        _tracker = new ElementTracker();
        _highlight = new HighlightOverlay(window, _tracker);
        _eventServer = new AgentEventServer($"snaipe-{Environment.ProcessId}-events");
    }

    /// <summary>
    /// Attach the agent to a running Uno window.
    /// </summary>
    public static SnaipeAgent Attach(Window window)
    {
        var agent = new SnaipeAgent(window);
        agent.Start();
        return agent;
    }

    private void Start()
    {
        try
        {
            _cts = new CancellationTokenSource();
            var pipeName = $"snaipe-{Environment.ProcessId}";

            Console.WriteLine($"[Snaipe.Agent] Starting agent with pipe name: {pipeName}");

            // Write discovery file (includes eventsPipeName).
            _discovery = AgentDiscovery.Create(pipeName, _window.Title ?? "Untitled");
            Console.WriteLine("[Snaipe.Agent] Discovery file created.");

            // Start command IPC server on a background task.
            _ipcServer = new AgentIpcServer(pipeName);
            _ = Task.Run(async () =>
            {
                try { await _ipcServer.RunAsync(HandleMessageAsync, _cts.Token); }
                catch (Exception ex) { Console.Error.WriteLine($"[Snaipe.Agent] IPC server error: {ex}"); }
            }, _cts.Token);

            // Start events push pipe on a background task.
            _ = Task.Run(async () =>
            {
                try { await _eventServer.RunAsync(_cts.Token); }
                catch (Exception ex) { Console.Error.WriteLine($"[Snaipe.Agent] Event server error: {ex}"); }
            }, _cts.Token);

            // Attach PickModeManager on the UI thread once Window.Content is available.
            _window.DispatcherQueue.TryEnqueue(() =>
            {
                if (_window.Content is UIElement root)
                {
                    _pickMode = new PickModeManager(root, _tracker, _highlight, _eventServer);
                    _pickMode.Attach();
                    Console.WriteLine("[Snaipe.Agent] PickModeManager attached (Ctrl+Shift to pick).");
                }
                else
                {
                    Console.WriteLine("[Snaipe.Agent] Window.Content not set — PickModeManager skipped.");
                }
            });

            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

            Console.WriteLine($"[Snaipe.Agent] Attached to window. Pipe: {pipeName}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Snaipe.Agent] Failed to start: {ex}");
        }
    }

    private async Task<InspectorMessage> HandleMessageAsync(
        InspectorMessage request, CancellationToken ct)
    {
        return request switch
        {
            GetTreeRequest => await HandleGetTree(request),
            GetPropertiesRequest gpr => await HandleGetProperties(gpr),
            SetPropertyRequest spr => await HandleSetProperty(spr),
            HighlightElementRequest her => await HandleHighlight(her),
            _ => new ErrorResponse
            {
                MessageId = request.MessageId,
                ErrorCode = ErrorCodes.UnknownMessage,
                Error = "Unknown message type",
                Details = $"Received: {request.GetType().Name}"
            }
        };
    }

    private Task<InspectorMessage> HandleGetTree(InspectorMessage request)
    {
        var tcs = new TaskCompletionSource<InspectorMessage>();

        _window.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                if (_window.Content is UIElement root)
                {
                    var xamlRoot = root.XamlRoot;
                    var roots = xamlRoot is not null
                        ? VisualTreeWalker.BuildTree(root, xamlRoot, _tracker)
                        : [VisualTreeWalker.BuildTree(root, _tracker)];

                    tcs.SetResult(new TreeResponse
                    {
                        MessageId = request.MessageId,
                        Roots = roots,
                    });
                }
                else
                {
                    tcs.SetResult(new ErrorResponse
                    {
                        MessageId = request.MessageId,
                        ErrorCode = ErrorCodes.InternalError,
                        Error = "Window has no content",
                    });
                }
            }
            catch (Exception ex)
            {
                tcs.SetResult(new ErrorResponse
                {
                    MessageId = request.MessageId,
                    ErrorCode = ErrorCodes.InternalError,
                    Error = "Tree walk failed",
                    Details = ex.Message,
                });
            }
        });

        return tcs.Task;
    }

    private Task<InspectorMessage> HandleGetProperties(GetPropertiesRequest request)
    {
        var tcs = new TaskCompletionSource<InspectorMessage>();

        _window.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                if (!_tracker.TryGetElement(request.ElementId, out var element) || element is null)
                {
                    tcs.SetResult(new ErrorResponse
                    {
                        MessageId = request.MessageId,
                        ErrorCode = ErrorCodes.ElementNotFound,
                        Error = "Element not found",
                        Details = $"ID: {request.ElementId}",
                    });
                    return;
                }

                List<Protocol.PropertyEntry> properties;

                if (request.PropertyPath is { Length: > 0 })
                {
                    var (resolved, errorCode, errorMessage) =
                        PropertyPathResolver.Resolve(element, request.PropertyPath);

                    if (resolved is null)
                    {
                        tcs.SetResult(new ErrorResponse
                        {
                            MessageId = request.MessageId,
                            ErrorCode = errorCode,
                            Error = errorMessage ?? "Path resolution failed",
                        });
                        return;
                    }

                    properties = ObjectPropertyReader.GetProperties(resolved);
                }
                else
                {
                    properties = PropertyReader.GetProperties(element);

                    if (_window.Content is UIElement root)
                    {
                        var bounds = VisualTreeWalker.GetBoundsRelativeTo(element, root);
                        properties.Insert(0, new Protocol.PropertyEntry
                        {
                            Name = "Bounds (X)", Category = "Layout", ValueType = "Double",
                            Value = bounds.X.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ValueKind = "Number", IsReadOnly = true,
                        });
                        properties.Insert(1, new Protocol.PropertyEntry
                        {
                            Name = "Bounds (Y)", Category = "Layout", ValueType = "Double",
                            Value = bounds.Y.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            ValueKind = "Number", IsReadOnly = true,
                        });
                    }
                }

                tcs.SetResult(new PropertiesResponse
                {
                    MessageId = request.MessageId,
                    ElementId = request.ElementId,
                    Properties = properties,
                });
            }
            catch (Exception ex)
            {
                tcs.SetResult(new ErrorResponse
                {
                    MessageId = request.MessageId,
                    ErrorCode = ErrorCodes.InternalError,
                    Error = "Property read failed",
                    Details = ex.Message,
                });
            }
        });

        return tcs.Task;
    }

    private Task<InspectorMessage> HandleSetProperty(SetPropertyRequest request)
    {
        var tcs = new TaskCompletionSource<InspectorMessage>();

        _window.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                if (!_tracker.TryGetElement(request.ElementId, out var element) || element is null)
                {
                    tcs.SetResult(new ErrorResponse
                    {
                        MessageId = request.MessageId,
                        ErrorCode = ErrorCodes.ElementNotFound,
                        Error = "Element not found",
                        Details = $"ID: {request.ElementId}",
                    });
                    return;
                }

                SetPropertyResult result;

                if (request.PropertyPath is { Length: > 0 })
                {
                    var (resolved, errorCode, errorMessage) =
                        PropertyPathResolver.Resolve(element, request.PropertyPath);

                    if (resolved is null)
                    {
                        tcs.SetResult(new ErrorResponse
                        {
                            MessageId = request.MessageId,
                            ErrorCode = errorCode,
                            Error = errorMessage ?? "Path resolution failed",
                        });
                        return;
                    }

                    result = ObjectPropertyWriter.SetProperty(resolved, request.PropertyName, request.NewValue);
                }
                else
                {
                    result = PropertyWriter.SetProperty(element, request.PropertyName, request.NewValue);
                }

                if (!result.Success)
                {
                    tcs.SetResult(new ErrorResponse
                    {
                        MessageId = request.MessageId,
                        ErrorCode = result.ErrorCode,
                        Error = result.Error ?? "Error",
                        Details = result.Details,
                    });
                    return;
                }

                tcs.SetResult(new AckResponse
                {
                    MessageId = request.MessageId,
                    NormalizedValue = result.NormalizedValue,
                });
            }
            catch (Exception ex)
            {
                tcs.SetResult(new ErrorResponse
                {
                    MessageId = request.MessageId,
                    ErrorCode = ErrorCodes.InternalError,
                    Error = "Property write failed",
                    Details = ex.Message,
                });
            }
        });

        return tcs.Task;
    }

    private Task<InspectorMessage> HandleHighlight(HighlightElementRequest request)
    {
        var tcs = new TaskCompletionSource<InspectorMessage>();

        _window.DispatcherQueue.TryEnqueue(() =>
        {
            try
            {
                _highlight.SetHighlight(request.ElementId, request.Show);
                tcs.SetResult(new AckResponse { MessageId = request.MessageId });
            }
            catch (Exception ex)
            {
                tcs.SetResult(new ErrorResponse
                {
                    MessageId = request.MessageId,
                    ErrorCode = ErrorCodes.InternalError,
                    Error = "Highlight failed",
                    Details = ex.Message,
                });
            }
        });

        return tcs.Task;
    }

    private void OnProcessExit(object? sender, EventArgs e) => Dispose();

    /// <summary>Snapshot the current visual tree.</summary>
    public ElementNode? GetTree()
    {
        if (_window.Content is UIElement root)
            return VisualTreeWalker.BuildTree(root, _tracker);
        return null;
    }

    public void Dispose()
    {
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        _cts?.Cancel();

        // Detach pick mode on UI thread (best effort).
        if (_pickMode is not null)
        {
            var pm = _pickMode;
            _window.DispatcherQueue.TryEnqueue(() => pm.Detach());
            _pickMode = null;
        }

        _highlight.Dispose();
        _eventServer.Dispose();
        _ipcServer?.Dispose();
        _discovery?.Dispose();
        _tracker.Dispose();
        _cts?.Dispose();
    }
}

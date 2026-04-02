// samples/Snaipe.SampleApp/ViewModels/ShellViewModel.cs
using Microsoft.UI.Dispatching;
using Snaipe.SampleApp.Services;

namespace Snaipe.SampleApp.ViewModels;

public sealed class ShellViewModel : ViewModelBase, IDisposable
{
    private ConversionState _state = ConversionState.Empty;
    private bool _isDocked = true;
    private string _statusMessage = "Ready — open an image to begin";
    private CancellationTokenSource? _conversionCts;

    private readonly ConversionPipeline _pipeline = new();
    private readonly DispatcherQueue _dispatcher;

    public ImagePanelViewModel          ImagePanel   { get; } = new();
    public AsciiOutputViewModel         AsciiOutput  { get; } = new();
    public ConversionSettingsViewModel  Settings     { get; } = new();
    public ExportViewModel              Export       { get; } = new();

    public ConversionState State { get => _state; private set { SetField(ref _state, value); SyncPanels(); } }
    public bool IsDocked         { get => _isDocked;       private set => SetField(ref _isDocked, value); }
    public string StatusMessage  { get => _statusMessage;  private set => SetField(ref _statusMessage, value); }

    public IEnumerable<ColorMode> ColorModes => Enum.GetValues<ColorMode>();
    public IEnumerable<DitheringAlgorithm> DitheringAlgs => Enum.GetValues<DitheringAlgorithm>();

    public AsyncRelayCommand   OpenImageCommand          { get; }
    public AsyncRelayCommand   LoadImageDirectCommand    { get; }
    public RelayCommand        TogglePreviewWindowCommand { get; }
    public RelayCommand        ShowExportDialogCommand   { get; }
    public AsyncRelayCommand   ConvertCommand            { get; }
    public RelayCommand        CopyToClipboardCommand    { get; }

    // Set by the view to open/close the preview window
    public Action? RequestOpenPreviewWindow  { get; set; }
    public Action? RequestClosePreviewWindow { get; set; }
    public Action? RequestShowExportDialog   { get; set; }

    public ShellViewModel(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;

        OpenImageCommand = new AsyncRelayCommand(OpenImageAsync);
        LoadImageDirectCommand = new AsyncRelayCommand(path => LoadImageDirectAsync((string)path!));
        ConvertCommand   = new AsyncRelayCommand(
            () => ConvertAsync(ImagePanel.FilePath, Settings.ToSettings()),
            () => !string.IsNullOrEmpty(ImagePanel.FilePath));

        TogglePreviewWindowCommand = new RelayCommand(() =>
        {
            if (IsDocked) RequestOpenPreviewWindow?.Invoke();
            else RequestClosePreviewWindow?.Invoke();
        });

        ShowExportDialogCommand = new RelayCommand(
            () => RequestShowExportDialog?.Invoke(),
            () => State.Document is not null);

        CopyToClipboardCommand = new RelayCommand(() =>
        {
            if (State.Document is null) return;
            var pkg = new global::Windows.ApplicationModel.DataTransfer.DataPackage();
            pkg.SetText(State.Document.ToPlainText());
            global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(pkg);
        }, () => State.Document is not null);

        Settings.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(ImagePanel.FilePath))
            _ = ConvertAsync(ImagePanel.FilePath, Settings.ToSettings());
    }

    private async Task OpenImageAsync()
    {
        var picker = new global::Windows.Storage.Pickers.FileOpenPicker();
        picker.FileTypeFilter.Add(".jpg");
        picker.FileTypeFilter.Add(".jpeg");
        picker.FileTypeFilter.Add(".png");
        picker.FileTypeFilter.Add(".bmp");
        picker.FileTypeFilter.Add(".gif");
        picker.FileTypeFilter.Add(".webp");

        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        await LoadImageDirectAsync(file.Path);
    }

    public async Task LoadImageDirectAsync(string path)
    {
        await ImagePanel.LoadImageAsync(path);
        ConvertCommand.RaiseCanExecuteChanged();
        StatusMessage = $"Loaded {System.IO.Path.GetFileName(path)}";
        await ConvertAsync(path, Settings.ToSettings());
    }

    public async Task ConvertAsync(string path, ConversionSettings settings)
    {
        if (string.IsNullOrEmpty(path)) return;

        _conversionCts?.Cancel();
        _conversionCts = new CancellationTokenSource();
        var ct = _conversionCts.Token;

        State = State with
        {
            Status = ConversionStatus.Converting,
            ProgressPercent = 0,
            ErrorMessage = null
        };
        StatusMessage = "Converting…";

        var progress = new Progress<int>(pct =>
            _dispatcher.TryEnqueue(() =>
            {
                State = State with { ProgressPercent = pct };
            }));

        try
        {
            var doc = await Task.Run(() => _pipeline.ConvertAsync(path, settings, progress, ct), ct);
            State = new ConversionState(path, settings, doc, ConversionStatus.Done, 100, null);
            StatusMessage = $"{System.IO.Path.GetFileName(path)} — {doc.Lines.Count} rows";
            Export.SetDocument(doc);
            ShowExportDialogCommand.RaiseCanExecuteChanged();
            CopyToClipboardCommand.RaiseCanExecuteChanged();
        }
        catch (OperationCanceledException)
        {
            // A new conversion was started — ignore
        }
        catch (Exception ex)
        {
            State = State with { Status = ConversionStatus.Error, ErrorMessage = ex.Message };
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    public void NotifyPreviewWindowOpened()  { IsDocked = false; }
    public void NotifyPreviewWindowClosed()  { IsDocked = true; }

    private void SyncPanels()
    {
        AsciiOutput.UpdateFromState(_state);
    }

    public void Dispose()
    {
        _conversionCts?.Dispose();
        _pipeline.Dispose();
    }
}

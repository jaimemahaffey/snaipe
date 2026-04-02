// samples/Snaipe.SampleApp/ViewModels/ExportViewModel.cs
using global::Windows.Storage.Pickers;

namespace Snaipe.SampleApp.ViewModels;

public enum ExportFormat { PlainText, HtmlColor, Clipboard }

public sealed class ExportViewModel : ViewModelBase
{
    private ExportFormat _selectedFormat = ExportFormat.PlainText;
    private string _destinationPath = string.Empty;
    private AsciiDocument? _document;

    public ExportFormat SelectedFormat   { get => _selectedFormat;  set => SetField(ref _selectedFormat, value); }
    public string DestinationPath        { get => _destinationPath; set => SetField(ref _destinationPath, value); }

    public AsyncRelayCommand BrowseCommand  { get; }
    public AsyncRelayCommand ExportCommand  { get; }

    public ExportViewModel()
    {
        BrowseCommand = new AsyncRelayCommand(BrowseAsync);
        ExportCommand = new AsyncRelayCommand(ExportAsync, () => _document is not null);
    }

    public void SetDocument(AsciiDocument? doc)
    {
        _document = doc;
        ExportCommand.RaiseCanExecuteChanged();
    }

    private async Task BrowseAsync()
    {
        var picker = new FileSavePicker();
        picker.SuggestedFileName = "ascii-art";
        if (SelectedFormat == ExportFormat.PlainText)
        {
            picker.FileTypeChoices.Add("Text file", [".txt"]);
            picker.SuggestedFileName += ".txt";
        }
        else if (SelectedFormat == ExportFormat.HtmlColor)
        {
            picker.FileTypeChoices.Add("HTML file", [".html"]);
            picker.SuggestedFileName += ".html";
        }
        // InitializeWithWindow not needed on WinUI3/Uno Win32 for file pickers
        var file = await picker.PickSaveFileAsync();
        if (file is not null) DestinationPath = file.Path;
    }

    private async Task ExportAsync()
    {
        if (_document is null) return;

        if (SelectedFormat == ExportFormat.Clipboard)
        {
            var dataPackage = new global::Windows.ApplicationModel.DataTransfer.DataPackage();
            dataPackage.SetText(_document.ToPlainText());
            global::Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            return;
        }

        if (string.IsNullOrEmpty(DestinationPath)) return;

        string content = SelectedFormat switch
        {
            ExportFormat.PlainText => _document.ToPlainText(),
            ExportFormat.HtmlColor => BuildHtml(_document),
            _ => _document.ToPlainText()
        };
        await System.IO.File.WriteAllTextAsync(DestinationPath, content);
    }

    private static string HtmlEncode(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    private static string BuildHtml(AsciiDocument doc)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><body style=\"background:#000;margin:0\"><pre style=\"font-family:monospace;font-size:12px;line-height:1.2\">");
        foreach (var line in doc.Lines)
        {
            foreach (var span in line.Spans)
            {
                if (span.Color is { } c)
                    sb.Append($"<span style=\"color:#{c.R:X2}{c.G:X2}{c.B:X2}\">{HtmlEncode(span.Text)}</span>");
                else
                    sb.Append(HtmlEncode(span.Text));
            }
            sb.AppendLine();
        }
        sb.AppendLine("</pre></body></html>");
        return sb.ToString();
    }
}

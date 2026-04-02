// samples/Snaipe.SampleApp/Windows/AsciiPreviewWindow.xaml.cs
using Microsoft.UI.Xaml;
using Snaipe.SampleApp.ViewModels;
using System.Linq;

namespace Snaipe.SampleApp.Windows;

public sealed partial class AsciiPreviewWindow : Window
{
    private readonly AsciiOutputViewModel _vm;

    public AsciiPreviewWindow(AsciiOutputViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        Title = "ASCII Studio — Preview";

        // Sync initial state
        SyncFromVm();

        // Subscribe to VM changes
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(AsciiOutputViewModel.Document))
            {
                PreviewOutput.Document = _vm.Document;
                UpdateInfoText();
            }
            if (e.PropertyName is nameof(AsciiOutputViewModel.DisplayStatus))
                PreviewOutput.Status = _vm.DisplayStatus;
            if (e.PropertyName is nameof(AsciiOutputViewModel.ProgressPercent))
                PreviewOutput.ProgressPercent = _vm.ProgressPercent;
            if (e.PropertyName is nameof(AsciiOutputViewModel.ErrorMessage))
                PreviewOutput.ErrorMessage = _vm.ErrorMessage;
        };

        // Font size slider
        FontSizeSlider.Value = _vm.OutputFontSize;
        FontSizeSlider.ValueChanged += (_, e) =>
        {
            _vm.OutputFontSize = e.NewValue;
            PreviewOutput.OutputFontSize = e.NewValue;
        };
    }

    private void SyncFromVm()
    {
        PreviewOutput.Document       = _vm.Document;
        PreviewOutput.Status         = _vm.DisplayStatus;
        PreviewOutput.ProgressPercent = _vm.ProgressPercent;
        PreviewOutput.ErrorMessage   = _vm.ErrorMessage;
        PreviewOutput.OutputFontSize  = _vm.OutputFontSize;
        FontSizeSlider.Value         = _vm.OutputFontSize;
        UpdateInfoText();
    }

    private void UpdateInfoText()
    {
        if (_vm.Document is { Lines.Count: > 0 } doc)
        {
            int cols = doc.Lines.Max(l => l.Spans.Sum(s => s.Text.Length));
            InfoText.Text = $"{doc.Lines.Count} rows × {cols} cols";
        }
        else
        {
            InfoText.Text = "No output";
        }
    }
}

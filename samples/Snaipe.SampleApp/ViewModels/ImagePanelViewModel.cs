// samples/Snaipe.SampleApp/ViewModels/ImagePanelViewModel.cs
using Microsoft.UI.Xaml.Media.Imaging;

namespace Snaipe.SampleApp.ViewModels;

public sealed class ImagePanelViewModel : ViewModelBase
{
    private BitmapImage? _sourceImage;
    private double _zoomLevel = 1.0;
    private string _filePath = string.Empty;

    public BitmapImage? SourceImage { get => _sourceImage; private set => SetField(ref _sourceImage, value); }
    public double ZoomLevel { get => _zoomLevel; set => SetField(ref _zoomLevel, value); }
    public string FilePath { get => _filePath; private set => SetField(ref _filePath, value); }

    public RelayCommand ZoomInCommand  { get; }
    public RelayCommand ZoomOutCommand { get; }
    public RelayCommand ResetZoomCommand { get; }

    public ImagePanelViewModel()
    {
        ZoomInCommand    = new RelayCommand(() => ZoomLevel = Math.Min(8.0, ZoomLevel * 1.25));
        ZoomOutCommand   = new RelayCommand(() => ZoomLevel = Math.Max(0.1, ZoomLevel / 1.25));
        ResetZoomCommand = new RelayCommand(() => ZoomLevel = 1.0);
    }

    public async Task LoadImageAsync(string path)
    {
        FilePath = path;
        var bmp = new BitmapImage();
        using var stream = System.IO.File.OpenRead(path);
        await bmp.SetSourceAsync(stream.AsRandomAccessStream());
        SourceImage = bmp;
        ZoomLevel = 1.0;
    }
}

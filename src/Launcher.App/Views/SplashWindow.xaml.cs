using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Launcher.Core.Services;

namespace Launcher.App.Views;

public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        TryLoadLogo();
    }

    /// <summary>Updates the status label and switches the progress bar to determinate mode.</summary>
    public void SetDownloadProgress(DownloadProgress p)
    {
        Dispatcher.Invoke(() =>
        {
            IndeterminateBar.Visibility = Visibility.Collapsed;
            ProgressBar.Visibility = Visibility.Visible;
            ProgressBar.Value = p.ProgressPercent;

            var speed = p.SpeedBytesPerSecond > 0
                ? $"  {DownloadService.FormatSpeed(p.SpeedBytesPerSecond)}"
                : string.Empty;
            StatusText.Text = $"Downloading update... {p.ProgressPercent:F0}%{speed}";
        });
    }

    /// <summary>Shows a plain status message (indeterminate bar).</summary>
    public void SetStatus(string message)
    {
        Dispatcher.Invoke(() =>
        {
            ProgressBar.Visibility = Visibility.Collapsed;
            IndeterminateBar.Visibility = Visibility.Visible;
            StatusText.Text = message;
        });
    }

    private void TryLoadLogo()
    {
        var baseDir = AppContext.BaseDirectory;
        string[] exts = [".png", ".jpg", ".jpeg", ".webp"];

        BitmapImage? img = null;
        foreach (var ext in exts)
        {
            var path = Path.Combine(baseDir, "Assets", $"logo{ext}");
            if (!File.Exists(path)) continue;
            try { img = EmbeddedImageLoader.LoadFromFile(path); } catch { }
            if (img != null) break;
        }

        img ??= EmbeddedImageLoader.LoadFromResource("Assets/logo.png");

        if (img == null) return;

        LogoImage.Source = img;
        LogoImage.Visibility = Visibility.Visible;
        LogoText.Visibility = Visibility.Collapsed;
    }
}

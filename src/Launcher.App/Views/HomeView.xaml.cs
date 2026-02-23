using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace Launcher.App.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }

    private void HomeView_Loaded(object sender, RoutedEventArgs e)
    {
        // Load banner image if it exists
        LoadBannerImage();

        // Trigger fade-in + slide-up animation
        var sb = (Storyboard)FindResource("FadeInStoryboard");
        sb.Begin();
    }

    /// <summary>
    /// Checks for Assets/banner.png (or .jpg/.webp) in the app directory.
    /// If found, shows the image and hides the text fallback.
    /// To customise: just drop your banner image into the Assets folder as "banner.png".
    /// </summary>
    private void LoadBannerImage()
    {
        var baseDir = AppContext.BaseDirectory;
        string[] extensions = [".png", ".jpg", ".jpeg", ".webp"];

        foreach (var ext in extensions)
        {
            var bannerPath = Path.Combine(baseDir, "Assets", $"banner{ext}");
            if (!File.Exists(bannerPath)) continue;

            try
            {
                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = new Uri(bannerPath, UriKind.Absolute);
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                img.Freeze();

                BannerImage.Source = img;
                BannerImage.Visibility = Visibility.Visible;
                BannerTextFallback.Visibility = Visibility.Collapsed;
                return;
            }
            catch
            {
                // If image fails to load, keep the text fallback
            }
        }
    }
}

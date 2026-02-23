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
    /// Loads the banner from disk (Assets/banner.*) or embedded resource.
    /// Disk file wins if present, allowing user overrides.
    /// </summary>
    private void LoadBannerImage()
    {
        var baseDir = AppContext.BaseDirectory;
        string[] extensions = [".png", ".jpg", ".jpeg", ".webp"];

        BitmapImage? img = null;

        // Try disk files first
        foreach (var ext in extensions)
        {
            var bannerPath = Path.Combine(baseDir, "Assets", $"banner{ext}");
            if (File.Exists(bannerPath))
            {
                try { img = EmbeddedImageLoader.LoadFromFile(bannerPath); } catch { }
                if (img != null) break;
            }
        }

        // Fall back to embedded resource
        img ??= EmbeddedImageLoader.LoadFromResource("Assets/banner.png");

        if (img == null) return;

        BannerImage.Source = img;
        BannerImage.Visibility = Visibility.Visible;
        BannerTextFallback.Visibility = Visibility.Collapsed;
    }
}

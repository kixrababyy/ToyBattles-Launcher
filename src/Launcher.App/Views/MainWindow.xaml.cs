using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Launcher.App.ViewModels;
using Microsoft.Win32;

namespace Launcher.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    // Wallpaper slideshow state
    private readonly DispatcherTimer _wallpaperTimer = new();
    private List<string> _wallpapers = new();
    private int _currentIndex = 0;
    private bool _showingA = true;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        // Wire up the folder browse request from Settings
        _viewModel.SettingsVM.BrowseFolderRequested += OnBrowseFolder;

        // Wire up game root request from Home
        _viewModel.HomeVM.OnGameRootRequested += OnGameRootRequested;

        Loaded += async (_, _) =>
        {
            await _viewModel.InitializeAsync();
            InitWallpaperSlideshow();
            LoadLogoImage();
        };
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    // ──────────────────────────────────────────────
    //  Logo image loading
    // ──────────────────────────────────────────────

    /// <summary>
    /// Checks for Assets/logo.png (or .jpg/.webp) in the app directory.
    /// If found, shows the image in the sidebar and hides the "TB" text fallback.
    /// To customise: drop your logo image into the Assets folder as "logo.png".
    /// </summary>
    private void LoadLogoImage()
    {
        var baseDir = AppContext.BaseDirectory;
        string[] extensions = [".png", ".jpg", ".jpeg", ".webp"];

        foreach (var ext in extensions)
        {
            var logoPath = Path.Combine(baseDir, "Assets", $"logo{ext}");
            if (!File.Exists(logoPath)) continue;

            try
            {
                var img = LoadImage(logoPath);

                // The Image and TextBlock are inside a ControlTemplate,
                // so we need to find them through the visual tree
                var logoButton = FindName("LogoButton") as System.Windows.Controls.Button;
                // Since we can't FindName inside templates easily,
                // walk the visual tree after template is applied
                Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        var logoImage = FindTemplateChild<System.Windows.Controls.Image>(this, "LogoImage");
                        var logoText = FindTemplateChild<System.Windows.Controls.TextBlock>(this, "LogoTextFallback");

                        if (logoImage != null)
                        {
                            logoImage.Source = img;
                            logoImage.Visibility = Visibility.Visible;
                        }
                        if (logoText != null)
                        {
                            logoText.Visibility = Visibility.Collapsed;
                        }
                    }
                    catch { /* Keep text fallback */ }
                }, System.Windows.Threading.DispatcherPriority.Loaded);
                return;
            }
            catch { /* Keep text fallback */ }
        }
    }

    /// <summary>
    /// Recursively searches the visual tree for a named element of a given type.
    /// </summary>
    private static T? FindTemplateChild<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        int childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T typed && typed.Name == name)
                return typed;

            var found = FindTemplateChild<T>(child, name);
            if (found != null)
                return found;
        }
        return null;
    }

    // ──────────────────────────────────────────────
    //  Wallpaper slideshow
    // ──────────────────────────────────────────────

    private void InitWallpaperSlideshow()
    {
        var wallpaperDir = Path.Combine(AppContext.BaseDirectory, "wallpapers");
        if (!Directory.Exists(wallpaperDir))
            return;

        _wallpapers = Directory
            .EnumerateFiles(wallpaperDir)
            .Where(f =>
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                return ext is ".png" or ".jpg" or ".jpeg" or ".webp";
            })
            .OrderBy(f => f)
            .ToList();

        if (_wallpapers.Count == 0)
            return;

        // Show first wallpaper immediately
        WallpaperA.Source = LoadImage(_wallpapers[0]);
        WallpaperA.Opacity = 1;

        if (_wallpapers.Count == 1)
            return;

        // Cycle every 9 seconds
        _wallpaperTimer.Interval = TimeSpan.FromSeconds(9);
        _wallpaperTimer.Tick += OnWallpaperTick;
        _wallpaperTimer.Start();
    }

    private void OnWallpaperTick(object? sender, EventArgs e)
    {
        _currentIndex = (_currentIndex + 1) % _wallpapers.Count;

        var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var duration = new Duration(TimeSpan.FromSeconds(1.8));

        if (_showingA)
        {
            WallpaperB.Source = LoadImage(_wallpapers[_currentIndex]);
            WallpaperB.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, duration) { EasingFunction = easing });
            WallpaperA.BeginAnimation(OpacityProperty,
                new DoubleAnimation(1, 0, duration) { EasingFunction = easing });
        }
        else
        {
            WallpaperA.Source = LoadImage(_wallpapers[_currentIndex]);
            WallpaperA.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, duration) { EasingFunction = easing });
            WallpaperB.BeginAnimation(OpacityProperty,
                new DoubleAnimation(1, 0, duration) { EasingFunction = easing });
        }

        _showingA = !_showingA;
    }

    private static BitmapImage LoadImage(string path)
    {
        var img = new BitmapImage();
        img.BeginInit();
        img.UriSource = new Uri(path, UriKind.Absolute);
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        img.EndInit();
        img.Freeze();
        return img;
    }

    // ──────────────────────────────────────────────
    //  Game folder dialogs
    // ──────────────────────────────────────────────

    private string? OnBrowseFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Game Directory (containing Bin\\MicroVolts.exe)"
        };

        if (dialog.ShowDialog() == true)
            return dialog.FolderName;

        return null;
    }

    private void OnGameRootRequested()
    {
        var folder = OnBrowseFolder();
        if (!string.IsNullOrEmpty(folder))
        {
            _viewModel.HomeVM.SetGameRoot(folder);
        }
    }
}

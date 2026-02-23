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
    private List<BitmapImage> _wallpapers = new();
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
    /// Loads the logo from disk (Assets/logo.png) or embedded resource.
    /// Disk file wins if present, allowing user overrides.
    /// </summary>
    private void LoadLogoImage()
    {
        var baseDir = AppContext.BaseDirectory;
        string[] extensions = [".png", ".jpg", ".jpeg", ".webp"];

        BitmapImage? img = null;

        // Try disk files first
        foreach (var ext in extensions)
        {
            var logoPath = Path.Combine(baseDir, "Assets", $"logo{ext}");
            if (File.Exists(logoPath))
            {
                try { img = EmbeddedImageLoader.LoadFromFile(logoPath); } catch { }
                if (img != null) break;
            }
        }

        // Fall back to embedded resource
        img ??= EmbeddedImageLoader.LoadFromResource("Assets/logo.png");

        if (img == null) return;

        var loadedImg = img;
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                var logoImage = FindTemplateChild<System.Windows.Controls.Image>(this, "LogoImage");
                var logoText = FindTemplateChild<System.Windows.Controls.TextBlock>(this, "LogoTextFallback");

                if (logoImage != null)
                {
                    logoImage.Source = loadedImg;
                    logoImage.Visibility = Visibility.Visible;
                }
                if (logoText != null)
                {
                    logoText.Visibility = Visibility.Collapsed;
                }
            }
            catch { /* Keep text fallback */ }
        }, DispatcherPriority.Loaded);
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
        _wallpapers = new List<BitmapImage>();

        // 1) Try disk wallpapers first (allows user override)
        var wallpaperDir = Path.Combine(AppContext.BaseDirectory, "wallpapers");
        if (Directory.Exists(wallpaperDir))
        {
            var files = Directory
                .EnumerateFiles(wallpaperDir)
                .Where(f =>
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    return ext is ".png" or ".jpg" or ".jpeg" or ".webp";
                })
                .OrderBy(f => f);

            foreach (var file in files)
            {
                try { _wallpapers.Add(EmbeddedImageLoader.LoadFromFile(file)); }
                catch { /* skip */ }
            }
        }

        // 2) If no disk wallpapers, use embedded resources
        if (_wallpapers.Count == 0)
        {
            var names = EmbeddedImageLoader.GetResourceNames("wallpapers/");
            foreach (var name in names)
            {
                var img = EmbeddedImageLoader.LoadFromResource(name);
                if (img != null)
                    _wallpapers.Add(img);
            }
        }

        if (_wallpapers.Count == 0)
            return;

        // Show first wallpaper immediately
        WallpaperA.Source = _wallpapers[0];
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
            WallpaperB.Source = _wallpapers[_currentIndex];
            WallpaperB.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, duration) { EasingFunction = easing });
            WallpaperA.BeginAnimation(OpacityProperty,
                new DoubleAnimation(1, 0, duration) { EasingFunction = easing });
        }
        else
        {
            WallpaperA.Source = _wallpapers[_currentIndex];
            WallpaperA.BeginAnimation(OpacityProperty,
                new DoubleAnimation(0, 1, duration) { EasingFunction = easing });
            WallpaperB.BeginAnimation(OpacityProperty,
                new DoubleAnimation(1, 0, duration) { EasingFunction = easing });
        }

        _showingA = !_showingA;
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

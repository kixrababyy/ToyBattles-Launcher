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
    private Hardcodet.Wpf.TaskbarNotification.TaskbarIcon? _trayIcon;

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

        // Wire up install folder picker request from Home and Repair
        _viewModel.HomeVM.OnInstallFolderRequested += OnInstallFolderRequested;
        _viewModel.RepairVM.OnInstallFolderRequested += OnInstallFolderRequested;

        // Restore window when game exits
        _viewModel.HomeVM.RestoreWindowRequested += RestoreFromTray;

        // Show tray balloon when update completes
        _viewModel.HomeVM.ShowBalloonRequested += (title, msg) =>
        {
            if (!IsVisible)
                _trayIcon?.ShowBalloonTip(title, msg, Hardcodet.Wpf.TaskbarNotification.BalloonIcon.Info);
        };

        // Confirm re-download when switching to a server with different game files (e.g. SEA)
        _viewModel.HomeVM.ConfirmRedownloadRequested += () =>
        {
            var result = MessageBox.Show(
                "The SEA server uses completely different game files.\n\n" +
                "You will need to download and install the SEA version separately.\n\n" +
                "Do you want to continue?",
                "Separate Download Required",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            return result == MessageBoxResult.Yes;
        };

        // Prompt user when a newer launcher version is available on GitHub
        _viewModel.HomeVM.LauncherUpdateAvailable += version =>
        {
            var result = MessageBox.Show(
                $"A new version of ToyBattles Launcher (v{version}) is available.\n\n" +
                "The launcher will restart automatically after downloading.\n\n" +
                "Update now?",
                "Launcher Update Available",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);
            return result == MessageBoxResult.Yes;
        };

        Loaded += async (_, _) =>
        {
            await _viewModel.InitializeAsync();
            InitWallpaperSlideshow();
            LoadLogoImage();
            InitTrayIcon();
            ApplyRoundClip();
            InnerGrid.SizeChanged += (_, _) => ApplyRoundClip();
        };

        // Clean up on close
        Closed += (_, _) =>
        {
            _trayIcon?.Dispose();
        };
    }

    private void ApplyRoundClip()
    {
        const double radius = 16;
        var geo = InnerGrid.Clip as System.Windows.Media.RectangleGeometry
               ?? new System.Windows.Media.RectangleGeometry();
        geo.Rect    = new Rect(0, 0, InnerGrid.ActualWidth, InnerGrid.ActualHeight);
        geo.RadiusX = radius;
        geo.RadiusY = radius;
        InnerGrid.Clip = geo;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Allow dragging from anywhere that isn't an interactive control
        if (e.ButtonState == MouseButtonState.Pressed && !IsInteractiveElement(e.OriginalSource))
            DragMove();
    }

    private static bool IsInteractiveElement(object source)
    {
        var el = source as DependencyObject;
        while (el != null)
        {
            if (el is System.Windows.Controls.Button
                or System.Windows.Controls.CheckBox
                or System.Windows.Controls.ComboBox
                or System.Windows.Controls.ComboBoxItem
                or System.Windows.Controls.TextBox
                or System.Windows.Controls.Primitives.ScrollBar
                or System.Windows.Controls.Slider
                or System.Windows.Controls.MenuItem
                or System.Windows.Controls.ScrollViewer)
                return true;
            el = System.Windows.Media.VisualTreeHelper.GetParent(el);
        }
        return false;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Kept for explicit title bar double-click minimise / other uses; drag handled at Window level
    }

    private void BannerOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _viewModel.NavigateHomeCommand.Execute(null);
        e.Handled = true;
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

        // Try disk files — valentine variant first, then default
        string[] names = ["logo_valentine", "logo"];
        foreach (var name in names)
        {
            foreach (var ext in extensions)
            {
                var logoPath = Path.Combine(baseDir, "Assets", $"{name}{ext}");
                if (File.Exists(logoPath))
                {
                    try { img = EmbeddedImageLoader.LoadFromFile(logoPath); } catch { }
                    if (img != null) break;
                }
            }
            if (img != null) break;
        }

        // Fall back to embedded resources — valentine first, then default
        img ??= EmbeddedImageLoader.LoadFromResource("Assets/logo_valentine.png");
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

        // 1) Try valentine wallpapers first, then regular wallpapers
        string[] wallpaperDirs = ["wallpapers-valentine", "wallpapers"];
        foreach (var dirName in wallpaperDirs)
        {
            var wallpaperDir = Path.Combine(AppContext.BaseDirectory, dirName);
            if (!Directory.Exists(wallpaperDir)) continue;

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

            if (_wallpapers.Count > 0) break;
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

        // Cycle every 5 seconds
        _wallpaperTimer.Interval = TimeSpan.FromSeconds(5);
        _wallpaperTimer.Tick += OnWallpaperTick;
        _wallpaperTimer.Start();
    }

    private void OnWallpaperTick(object? sender, EventArgs e)
    {
        _currentIndex = (_currentIndex + 1) % _wallpapers.Count;

        var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var duration = new Duration(TimeSpan.FromSeconds(0.8));

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

    private string? OnInstallFolderRequested()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Installation Directory"
        };

        if (dialog.ShowDialog() == true)
            return dialog.FolderName;

        return null;
    }

    private string? OnBrowseFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select ToyBattles Game Directory"
        };

        if (dialog.ShowDialog() == true)
            return dialog.FolderName;

        return null;
    }

    // ──────────────────────────────────────────────
    //  System Tray
    // ──────────────────────────────────────────────

    private void InitTrayIcon()
    {
        _trayIcon = new Hardcodet.Wpf.TaskbarNotification.TaskbarIcon
        {
            ToolTipText = "ToyBattles Launcher",
            Visibility = Visibility.Hidden
        };

        // Try to load embedded icon
        try
        {
            var iconStream = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Assets/icon.ico");
            if (iconStream != null)
            {
                var decoder = new System.Windows.Media.Imaging.IconBitmapDecoder(
                    iconStream,
                    System.Windows.Media.Imaging.BitmapCreateOptions.None,
                    System.Windows.Media.Imaging.BitmapCacheOption.Default);
                _trayIcon.IconSource = decoder.Frames[0];
            }
        }
        catch { /* no icon — tray icon will show default */ }

        // Double-click tray icon to restore
        _trayIcon.TrayMouseDoubleClick += (_, _) => RestoreFromTray();

        // Context menu: Restore + Exit
        var contextMenu = new System.Windows.Controls.ContextMenu();
        var openItem = new System.Windows.Controls.MenuItem { Header = "Open Launcher" };
        openItem.Click += (_, _) => RestoreFromTray();
        var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => Application.Current.Shutdown();
        contextMenu.Items.Add(openItem);
        contextMenu.Items.Add(new System.Windows.Controls.Separator());
        contextMenu.Items.Add(exitItem);
        _trayIcon.ContextMenu = contextMenu;
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        if (_trayIcon != null)
            _trayIcon.Visibility = Visibility.Hidden;
    }

}

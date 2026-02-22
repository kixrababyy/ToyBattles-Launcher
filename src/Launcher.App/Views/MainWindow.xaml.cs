using System.Windows;
using System.Windows.Input;
using Launcher.App.ViewModels;
using Microsoft.Win32;

namespace Launcher.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        // Wire up the folder browse request from Settings
        _viewModel.SettingsVM.BrowseFolderRequested += OnBrowseFolder;

        // Wire up game root request from Home
        _viewModel.HomeVM.OnGameRootRequested += OnGameRootRequested;

        Loaded += async (_, _) => await _viewModel.InitializeAsync();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

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

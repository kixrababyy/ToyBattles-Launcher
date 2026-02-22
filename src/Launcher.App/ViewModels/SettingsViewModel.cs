using System.Windows.Input;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.App.ViewModels;

/// <summary>
/// ViewModel for the Settings view.
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private LocalState _localState;

    private string _gameRootPath = string.Empty;
    public string GameRootPath
    {
        get => _gameRootPath;
        set
        {
            if (SetProperty(ref _gameRootPath, value))
                OnPropertyChanged(nameof(IsGameRootValid));
        }
    }

    private string _launchArguments = string.Empty;
    public string LaunchArguments
    {
        get => _launchArguments;
        set => SetProperty(ref _launchArguments, value);
    }

    private string _updateUrl = string.Empty;
    public string UpdateUrl
    {
        get => _updateUrl;
        set => SetProperty(ref _updateUrl, value);
    }

    private string _installedVersion = string.Empty;
    public string InstalledVersion
    {
        get => _installedVersion;
        set => SetProperty(ref _installedVersion, value);
    }

    public bool IsGameRootValid => LaunchService.ValidateGameRoot(GameRootPath);

    public ICommand BrowseGameRootCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand OpenLogsFolderCommand { get; }

    // Event to request folder browser from the View
    public event Func<string?>? BrowseFolderRequested;

    public SettingsViewModel()
    {
        _localState = LocalState.Load();
        LoadSettings();

        BrowseGameRootCommand = new RelayCommand(OnBrowseGameRoot);
        SaveSettingsCommand = new RelayCommand(OnSaveSettings);
        OpenLogsFolderCommand = new RelayCommand(_ => LogService.OpenLogsFolder());
    }

    private void LoadSettings()
    {
        GameRootPath = _localState.GameRootPath ?? string.Empty;
        LaunchArguments = _localState.LaunchArguments ?? string.Empty;
        UpdateUrl = _localState.CustomUpdateUrl ?? string.Empty;
        InstalledVersion = _localState.InstalledVersion ?? "Not installed";
    }

    private void OnBrowseGameRoot(object? _)
    {
        var folder = BrowseFolderRequested?.Invoke();
        if (!string.IsNullOrEmpty(folder))
        {
            GameRootPath = folder;
        }
    }

    private void OnSaveSettings(object? _)
    {
        _localState.GameRootPath = GameRootPath;
        _localState.LaunchArguments = LaunchArguments;
        _localState.CustomUpdateUrl = string.IsNullOrWhiteSpace(UpdateUrl) ? null : UpdateUrl;
        _localState.Save();

        LogService.Log("Settings saved.");
    }

    public void Refresh()
    {
        _localState = LocalState.Load();
        LoadSettings();
    }
}

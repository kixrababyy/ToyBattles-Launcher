using System.IO;
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
            {
                OnPropertyChanged(nameof(IsGameRootValid));
                AutoSave();
            }
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

    private bool _keepLauncherOpen;
    public bool KeepLauncherOpen
    {
        get => _keepLauncherOpen;
        set { if (SetProperty(ref _keepLauncherOpen, value)) AutoSave(); }
    }

    private bool _checkUpdatesOnStartup;
    public bool CheckUpdatesOnStartup
    {
        get => _checkUpdatesOnStartup;
        set { if (SetProperty(ref _checkUpdatesOnStartup, value)) AutoSave(); }
    }

    private string _maxDownloadSpeed = "0";
    public string MaxDownloadSpeed
    {
        get => _maxDownloadSpeed;
        set => SetProperty(ref _maxDownloadSpeed, value);
    }

    private string _serverIp = string.Empty;
    public string ServerIp
    {
        get => _serverIp;
        set { if (SetProperty(ref _serverIp, value)) AutoSave(); }
    }

    public bool IsGameRootValid => LaunchService.ValidateGameRoot(GameRootPath);

    public ICommand BrowseGameRootCommand { get; }
    public ICommand OpenGameFolderCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand DiscardChangesCommand { get; }
    public ICommand OpenLogsFolderCommand { get; }
    public ICommand CreateShortcutCommand { get; }

    // Event to request folder browser from the View
    public event Func<string?>? BrowseFolderRequested;

    public SettingsViewModel()
    {
        _localState = LocalState.Load();
        LoadSettings();

        BrowseGameRootCommand = new RelayCommand(OnBrowseGameRoot);
        OpenGameFolderCommand = new RelayCommand(_ =>
        {
            var path = GameRootPath;
            if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        });
        SaveSettingsCommand = new RelayCommand(OnSaveSettings);
        DiscardChangesCommand = new RelayCommand(_ => LoadSettings());
        OpenLogsFolderCommand = new RelayCommand(_ => LogService.OpenLogsFolder());
        CreateShortcutCommand = new RelayCommand(OnCreateShortcut);
    }

    private void LoadSettings()
    {
        var savedPath = _localState.GameRootPath ?? string.Empty;
        GameRootPath = LaunchService.ValidateGameRoot(savedPath) ? savedPath : string.Empty;
        LaunchArguments = _localState.LaunchArguments ?? string.Empty;
        UpdateUrl = _localState.CustomUpdateUrl ?? string.Empty;
        InstalledVersion = _localState.InstalledVersion ?? "Not installed";
        KeepLauncherOpen = _localState.KeepLauncherOpen;
        CheckUpdatesOnStartup = _localState.CheckUpdatesOnStartup;
        MaxDownloadSpeed = _localState.MaxDownloadSpeedMBps.ToString();
        ServerIp = _localState.ServerIp ?? string.Empty;

        // Apply speed limit immediately when settings load
        DownloadService.MaxBytesPerSecond = (long)_localState.MaxDownloadSpeedMBps * 1024 * 1024;
    }

    private void AutoSave()
    {
        _localState.KeepLauncherOpen = KeepLauncherOpen;
        _localState.ServerIp = string.IsNullOrWhiteSpace(ServerIp) ? null : ServerIp;
        _localState.Save();
    }

    private void OnBrowseGameRoot(object? _)
    {
        var folder = BrowseFolderRequested?.Invoke();
        if (!string.IsNullOrEmpty(folder))
        {
            GameRootPath = folder;
            _localState.GameRootPath = folder;
            _localState.Save();
        }
    }

    private void OnSaveSettings(object? _)
    {
        _localState.GameRootPath = GameRootPath;
        _localState.LaunchArguments = LaunchArguments;
        _localState.CustomUpdateUrl = string.IsNullOrWhiteSpace(UpdateUrl) ? null : UpdateUrl;
        _localState.KeepLauncherOpen = KeepLauncherOpen;
        _localState.CheckUpdatesOnStartup = CheckUpdatesOnStartup;

        if (int.TryParse(MaxDownloadSpeed, out int speed) && speed >= 0)
            _localState.MaxDownloadSpeedMBps = speed;
        else
            _localState.MaxDownloadSpeedMBps = 0;

        _localState.ServerIp = string.IsNullOrWhiteSpace(ServerIp) ? null : ServerIp;

        // Apply immediately
        DownloadService.MaxBytesPerSecond = (long)_localState.MaxDownloadSpeedMBps * 1024 * 1024;

        _localState.Save();
        LogService.Log("Settings saved.");
    }

    private void OnCreateShortcut(object? _)
    {
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var shortcutPath = Path.Combine(desktop, "ToyBattles Launcher.lnk");
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            if (string.IsNullOrEmpty(exePath)) return;

            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = exePath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty;
            shortcut.Description = "ToyBattles Launcher";
            shortcut.Save();

            LogService.Log($"Desktop shortcut created: {shortcutPath}");
        }
        catch (Exception ex)
        {
            LogService.LogError("Failed to create desktop shortcut", ex);
        }
    }

    public void Refresh()
    {
        _localState = LocalState.Load();
        LoadSettings();
    }
}

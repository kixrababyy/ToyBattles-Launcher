using System.IO;
using System.Windows.Input;
using Launcher.Core.Config;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.App.ViewModels;

public enum LauncherState
{
    Initializing,
    Checking,
    UpdateAvailable,
    Downloading,
    Applying,
    Ready,
    Launching,
    Error,
    NeedGameRoot
}

/// <summary>
/// ViewModel for the Home view — handles update checking, downloading, and launching.
/// </summary>
public class HomeViewModel : ViewModelBase
{
    private readonly PatchService _patchService = new();
    private readonly DownloadService _downloadService = new();
    private CancellationTokenSource? _cts;

    private LauncherState _state = LauncherState.Initializing;
    public LauncherState State
    {
        get => _state;
        set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(ActionButtonText));
                OnPropertyChanged(nameof(IsProgressVisible));
                OnPropertyChanged(nameof(IsActionEnabled));
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    private double _progressPercent;
    public double ProgressPercent
    {
        get => _progressPercent;
        set => SetProperty(ref _progressPercent, value);
    }

    private string _statusText = "Initializing...";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private string _downloadSpeed = string.Empty;
    public string DownloadSpeed
    {
        get => _downloadSpeed;
        set => SetProperty(ref _downloadSpeed, value);
    }

    private string _eta = string.Empty;
    public string Eta
    {
        get => _eta;
        set => SetProperty(ref _eta, value);
    }

    private string _patchNotes = "Welcome to ToyBattles!\n\nChecking for updates...";
    public string PatchNotes
    {
        get => _patchNotes;
        set => SetProperty(ref _patchNotes, value);
    }

    private string _installedVersionText = string.Empty;
    public string InstalledVersionText
    {
        get => _installedVersionText;
        set => SetProperty(ref _installedVersionText, value);
    }

    private string _remoteVersionText = string.Empty;
    public string RemoteVersionText
    {
        get => _remoteVersionText;
        set => SetProperty(ref _remoteVersionText, value);
    }

    public string ActionButtonText => State switch
    {
        LauncherState.Initializing => "INITIALIZING",
        LauncherState.Checking => "CHECKING...",
        LauncherState.UpdateAvailable => "UPDATE",
        LauncherState.Downloading => "DOWNLOADING...",
        LauncherState.Applying => "APPLYING...",
        LauncherState.Ready => "PLAY",
        LauncherState.Launching => "LAUNCHING...",
        LauncherState.Error => "RETRY",
        LauncherState.NeedGameRoot => "LOCATE GAME",
        _ => "..."
    };

    public bool IsProgressVisible => State is LauncherState.Downloading or LauncherState.Applying or LauncherState.Checking;
    public bool IsActionEnabled => State is LauncherState.UpdateAvailable or LauncherState.Ready
                                    or LauncherState.Error or LauncherState.NeedGameRoot;

    // Stored after check
    private PatchConfig? _remotePatch;
    private UpdateInfoConfig? _updateInfoConfig;
    private LocalState _localState = new();

    public ICommand ActionCommand { get; }

    public HomeViewModel()
    {
        ActionCommand = new AsyncRelayCommand(OnActionAsync);
    }

    /// <summary>
    /// Initialize: load state and check for updates.
    /// Called from MainViewModel after construction.
    /// </summary>
    public async Task InitializeAsync()
    {
        _localState = LocalState.Load();

        // Try to auto-detect game root if not set
        if (string.IsNullOrEmpty(_localState.GameRootPath) || !LaunchService.ValidateGameRoot(_localState.GameRootPath))
        {
            // Try the current working directory
            var cwd = AppDomain.CurrentDomain.BaseDirectory;
            // Walk up to find a directory containing Bin\MicroVolts.exe
            var candidate = FindGameRoot(cwd);
            if (candidate != null)
            {
                _localState.GameRootPath = candidate;
                _localState.Save();
            }
            else
            {
                State = LauncherState.NeedGameRoot;
                StatusText = "Game installation not found. Please locate your game folder.";
                return;
            }
        }

        // Load local patch.ini to get installed version
        LoadLocalVersion();

        await CheckForUpdatesAsync();
    }

    private void LoadLocalVersion()
    {
        if (string.IsNullOrEmpty(_localState.GameRootPath))
            return;

        var localPatchIni = Path.Combine(_localState.GameRootPath, "patch.ini");
        if (File.Exists(localPatchIni))
        {
            var localPatch = PatchConfig.Load(localPatchIni);
            _localState.InstalledVersion = localPatch.LatestVersion.ToString();
            InstalledVersionText = $"Installed: {localPatch.LatestVersion}";
        }

        // Load updateinfo.ini
        var updateInfoPath = Path.Combine(_localState.GameRootPath, "updateinfo.ini");
        if (File.Exists(updateInfoPath))
        {
            _updateInfoConfig = UpdateInfoConfig.Load(updateInfoPath);
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        State = LauncherState.Checking;
        StatusText = "Checking for updates...";
        ProgressPercent = 0;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        try
        {
            if (_updateInfoConfig == null || string.IsNullOrEmpty(_updateInfoConfig.UpdateAddress))
            {
                LogService.Log("No update URL configured.");
                State = LauncherState.Ready;
                StatusText = "Ready to play!";
                return;
            }

            _remotePatch = await _patchService.CheckForUpdateAsync(_updateInfoConfig.UpdateAddress, _cts.Token);

            if (_remotePatch == null)
            {
                LogService.Log("Could not reach update server.");
                // If we have a game to play, allow it
                if (LaunchService.ValidateGameRoot(_localState.GameRootPath ?? ""))
                {
                    State = LauncherState.Ready;
                    StatusText = "Could not reach update server. Ready to play (offline).";
                }
                else
                {
                    State = LauncherState.Error;
                    StatusText = "Could not reach update server.";
                }
                return;
            }

            RemoteVersionText = $"Latest: {_remotePatch.LatestVersion}";
            var installedVersion = _localState.GetInstalledVersion();

            LogService.Log($"Local version: {installedVersion}, Remote version: {_remotePatch.LatestVersion}");

            if (PatchService.NeedsUpdate(installedVersion, _remotePatch.LatestVersion))
            {
                State = LauncherState.UpdateAvailable;
                StatusText = $"Update available: {_remotePatch.LatestVersion}";
                PatchNotes = $"🎮 ToyBattles Update\n\n" +
                             $"Current version: {installedVersion}\n" +
                             $"New version: {_remotePatch.LatestVersion}\n\n" +
                             $"Click UPDATE to download and install the latest patch.";
            }
            else
            {
                LogService.Log($"Version {installedVersion} is up to date.");
                State = LauncherState.Ready;
                StatusText = "Game is up to date!";
                PatchNotes = $"🎮 ToyBattles\n\n" +
                             $"Version: {installedVersion}\n\n" +
                             $"Your game is up to date. Click PLAY to launch!";
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
        catch (Exception ex)
        {
            LogService.LogError("Error checking for updates", ex);
            State = LauncherState.Error;
            StatusText = $"Error: {ex.Message}";
        }
    }

    private async Task OnActionAsync()
    {
        switch (State)
        {
            case LauncherState.NeedGameRoot:
                // This will be handled by the MainViewModel via event
                OnGameRootRequested?.Invoke();
                break;

            case LauncherState.UpdateAvailable:
                await DownloadAndApplyUpdateAsync();
                break;

            case LauncherState.Ready:
                LaunchGame();
                break;

            case LauncherState.Error:
                await CheckForUpdatesAsync();
                break;
        }
    }

    private async Task DownloadAndApplyUpdateAsync()
    {
        if (_remotePatch == null || _updateInfoConfig == null || string.IsNullOrEmpty(_localState.GameRootPath))
            return;

        State = LauncherState.Downloading;
        _cts = new CancellationTokenSource();

        var progress = new Progress<DownloadProgress>(p =>
        {
            ProgressPercent = p.ProgressPercent;
            StatusText = p.StatusText;
            DownloadSpeed = DownloadService.FormatSpeed(p.SpeedBytesPerSecond);
            Eta = p.EstimatedTimeRemaining.TotalSeconds > 0
                ? $"ETA: {p.EstimatedTimeRemaining:mm\\:ss}"
                : string.Empty;
        });

        try
        {
            var installedVersion = _localState.GetInstalledVersion();
            var success = await _patchService.ApplyUpdateAsync(
                _localState.GameRootPath,
                _updateInfoConfig.UpdateAddress,
                installedVersion,
                _remotePatch,
                progress,
                _cts.Token);

            if (success)
            {
                _localState.SetInstalledVersion(_remotePatch.LatestVersion);
                _localState.Save();
                InstalledVersionText = $"Installed: {_remotePatch.LatestVersion}";

                State = LauncherState.Ready;
                StatusText = "Update complete! Ready to play.";
                ProgressPercent = 100;
                DownloadSpeed = string.Empty;
                Eta = string.Empty;

                PatchNotes = $"🎮 ToyBattles\n\n" +
                             $"Version: {_remotePatch.LatestVersion}\n\n" +
                             $"Update applied successfully! Click PLAY to launch.";
            }
            else
            {
                State = LauncherState.Error;
                StatusText = "Update failed. Check logs for details.";
            }
        }
        catch (OperationCanceledException)
        {
            State = LauncherState.UpdateAvailable;
            StatusText = "Update cancelled.";
        }
        catch (Exception ex)
        {
            LogService.LogError("Update failed", ex);
            State = LauncherState.Error;
            StatusText = $"Update failed: {ex.Message}";
        }
    }

    private void LaunchGame()
    {
        if (string.IsNullOrEmpty(_localState.GameRootPath))
            return;

        State = LauncherState.Launching;
        StatusText = "Launching ToyBattles...";

        var success = LaunchService.Launch(_localState.GameRootPath, _localState.LaunchArguments);
        if (success)
        {
            // Optionally close the launcher after launch
            StatusText = "Game launched!";
            State = LauncherState.Ready;
        }
        else
        {
            State = LauncherState.Error;
            StatusText = "Failed to launch game. Check if MicroVolts.exe exists.";
        }
    }

    public void SetGameRoot(string path)
    {
        _localState.GameRootPath = path;
        _localState.Save();
        LoadLocalVersion();
        _ = CheckForUpdatesAsync();
    }

    public event Action? OnGameRootRequested;

    private static string? FindGameRoot(string startDir)
    {
        var dir = startDir;
        for (int i = 0; i < 5; i++)
        {
            if (LaunchService.ValidateGameRoot(dir))
                return dir;

            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }

        return null;
    }
}

using System.IO;
using System.Net.Http;
using System.Windows.Input;
using System.Windows.Media;
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
    NeedGameRoot,
    Installing,
    VerifyingFiles
}

/// <summary>
/// ViewModel for the Home view — handles update checking, downloading, and launching.
/// </summary>
public class HomeViewModel : ViewModelBase
{
    private readonly PatchService _patchService = new();
    private readonly DownloadService _downloadService = new();
    private readonly InstallService _installService = new();
    private CancellationTokenSource? _cts;

    /// <summary>Default base URL for fetching updateinfo.ini during fresh install.</summary>
    private const string BootstrapUpdateInfoUrl = "http://cdn.toybattles.net/ENG/microvolts/patch.ini";

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
                OnPropertyChanged(nameof(IsCancelVisible));
                OnPropertyChanged(nameof(IsCheckUpdatesVisible));
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

    private string _downloadSizeText = string.Empty;
    public string DownloadSizeText
    {
        get => _downloadSizeText;
        set => SetProperty(ref _downloadSizeText, value);
    }

    private string _serverStatusText = "Checking...";
    public string ServerStatusText
    {
        get => _serverStatusText;
        set => SetProperty(ref _serverStatusText, value);
    }

    private string _playtimeText = string.Empty;
    public string PlaytimeText
    {
        get => _playtimeText;
        set => SetProperty(ref _playtimeText, value);
    }

    private bool _isVersionUpToDate;
    public bool IsVersionUpToDate
    {
        get => _isVersionUpToDate;
        set => SetProperty(ref _isVersionUpToDate, value);
    }

    private SolidColorBrush _serverStatusBrush = new(Color.FromRgb(0xFF, 0xB8, 0x00));
    public SolidColorBrush ServerStatusBrush
    {
        get => _serverStatusBrush;
        set => SetProperty(ref _serverStatusBrush, value);
    }

    private bool _isSettingsOpen;
    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => SetProperty(ref _isSettingsOpen, value);
    }

    public static string[] ServerOptions { get; } = ["Main Build", "Test Server"];

    private static readonly Dictionary<string, string> ServerAddresses = new()
    {
        ["Main Build"] = "https://cdn.toybattles.net/ENG",
        ["Test Server"] = "http://127.0.0.1",
    };

    private string GetServerAddress() =>
        ServerAddresses.TryGetValue(_selectedServer, out var addr) && !string.IsNullOrEmpty(addr)
            ? addr : "https://cdn.toybattles.net/ENG";

    /// <summary>Overrides _updateInfoConfig with URLs derived from the selected server profile.</summary>
    private void ApplyServerProfile()
    {
        var addr = GetServerAddress();
        _updateInfoConfig ??= new UpdateInfoConfig();
        _updateInfoConfig.UpdateAddress = addr;
        _updateInfoConfig.FullFileAddress = $"{addr}/microvolts/Full/Full.zip";
    }

    private string _selectedServer = "Main Build";
    public string SelectedServer
    {
        get => _selectedServer;
        set
        {
            if (SetProperty(ref _selectedServer, value))
            {
                _localState.ServerProfile = value;
                _localState.Save();
                ApplyServerProfile();
            }
        }
    }

    public string ActionButtonText => State switch
    {
        LauncherState.Initializing => "INITIALIZING",
        LauncherState.Checking => "CHECKING...",
        LauncherState.UpdateAvailable => "UPDATE",
        LauncherState.Downloading => "DOWNLOADING...",
        LauncherState.Applying => "APPLYING...",
        LauncherState.Installing => "INSTALLING...",
        LauncherState.VerifyingFiles => "VERIFYING...",
        LauncherState.Ready => "PLAY",
        LauncherState.Launching => "LAUNCHING...",
        LauncherState.Error => "RETRY",
        LauncherState.NeedGameRoot => "INSTALL",
        _ => "..."
    };

    public bool IsProgressVisible => State is LauncherState.Downloading or LauncherState.Applying
                                     or LauncherState.Checking or LauncherState.Installing
                                     or LauncherState.VerifyingFiles;
    public bool IsActionEnabled => State is LauncherState.UpdateAvailable or LauncherState.Ready
                                    or LauncherState.Error or LauncherState.NeedGameRoot;
    public bool IsCancelVisible => State is LauncherState.Downloading or LauncherState.Installing
                                    or LauncherState.Applying;
    public bool IsCheckUpdatesVisible => State is LauncherState.Ready or LauncherState.Error;

    // Stored after check
    private PatchConfig? _remotePatch;
    private UpdateInfoConfig? _updateInfoConfig;
    private LocalState _localState = new();

    public ICommand ActionCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand CheckUpdatesCommand { get; }
    public ICommand ToggleSettingsCommand { get; }

    /// <summary>Fired when the game exits and the launcher should restore from tray.</summary>
    public event Action? RestoreWindowRequested;

    /// <summary>Fired when a tray balloon notification should be shown.</summary>
    public event Action<string, string>? ShowBalloonRequested;

    public HomeViewModel()
    {
        ActionCommand = new AsyncRelayCommand(OnActionAsync);
        CancelCommand = new RelayCommand(_ => _cts?.Cancel());
        CheckUpdatesCommand = new AsyncRelayCommand(async () =>
        {
            DownloadSizeText = string.Empty;
            await CheckForUpdatesAsync();
        });
        ToggleSettingsCommand = new RelayCommand(_ => IsSettingsOpen = !IsSettingsOpen);
    }

    /// <summary>
    /// Initialize: load state and check for updates.
    /// Called from MainViewModel after construction.
    /// </summary>
    public async Task InitializeAsync()
    {
        _localState = LocalState.Load();

        // Recover playtime from any session where the launcher was closed with the game running
        RecoverPendingSession();
        UpdatePlaytimeText();

        // Load updateinfo.ini — try local file first, then bootstrap from CDN
        var cwd = AppDomain.CurrentDomain.BaseDirectory;
        var localUpdateInfo = Path.Combine(cwd, "updateinfo.ini");
        if (File.Exists(localUpdateInfo))
        {
            _updateInfoConfig = UpdateInfoConfig.Load(localUpdateInfo);
        }

        // If no local updateinfo.ini, try fetching from bootstrap URL
        if (_updateInfoConfig == null || string.IsNullOrEmpty(_updateInfoConfig.FullFileAddress))
        {
            try
            {
                LogService.Log("No local updateinfo.ini found, fetching from CDN...");
                _updateInfoConfig = await _installService.FetchUpdateInfoAsync(BootstrapUpdateInfoUrl);
            }
            catch (Exception ex)
            {
                LogService.LogError("Failed to fetch updateinfo.ini from CDN", ex);
            }
        }

        // Restore selected server profile and apply URLs — must happen before NeedGameRoot check
        // so fresh installs use the correct FullFileAddress
        if (!string.IsNullOrEmpty(_localState.ServerProfile) &&
            Array.IndexOf(ServerOptions, _localState.ServerProfile) >= 0)
            _selectedServer = _localState.ServerProfile;

        ApplyServerProfile();

        // Validate saved game root — clear if stale
        if (!string.IsNullOrEmpty(_localState.GameRootPath) && !ValidateCriticalFiles(_localState.GameRootPath))
        {
            LogService.Log($"Stale game root path cleared: {_localState.GameRootPath}");
            _localState.GameRootPath = null;
            _localState.Save();
        }

        if (string.IsNullOrEmpty(_localState.GameRootPath))
        {
            State = LauncherState.NeedGameRoot;
            StatusText = "Game not installed. Click INSTALL to download and set up the game.";
            return;
        }

        // Load local patch.ini to get installed version
        LoadLocalVersion();

        // Ping server status in background
        _ = DoPingServerStatusAsync();

        await CheckForUpdatesAsync();

        // Verify cgd.dip against remote (Adler32) in background after update check
        _ = CheckCgdDipAsync();
    }

    /// <summary>
    /// Checks that critical game files exist beyond just the exe.
    /// </summary>
    private static string FormatPlaytime(long totalSeconds)
    {
        if (totalSeconds <= 0) return "0m played";
        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        if (hours == 0 && minutes == 0) return "< 1m played";
        if (hours == 0) return $"{minutes}m played";
        if (minutes == 0) return $"{hours}h played";
        return $"{hours}h {minutes}m played";
    }

    private void UpdatePlaytimeText() =>
        PlaytimeText = FormatPlaytime(_localState.TotalPlaytimeSeconds);

    private void RecoverPendingSession()
    {
        if (_localState.PendingSessionStartUtc is not { } start) return;

        // Cap at 12 hours to avoid inflating playtime if the PC was left on overnight
        const long maxSessionSeconds = 12 * 3600;
        var elapsed = (long)(DateTime.UtcNow - start).TotalSeconds;
        elapsed = Math.Clamp(elapsed, 0, maxSessionSeconds);

        _localState.TotalPlaytimeSeconds += elapsed;
        _localState.PendingSessionStartUtc = null;
        _localState.Save();
        LogService.Log($"Recovered pending session: +{elapsed}s playtime");
    }

    private static bool ValidateCriticalFiles(string gameRootPath)
    {
        if (!LaunchService.ValidateGameRoot(gameRootPath))
            return false;

        // Check for critical data file
        var dataFile = Path.Combine(gameRootPath, "data", "cgd.dip");
        return File.Exists(dataFile);
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
                IsVersionUpToDate = false;
                State = LauncherState.UpdateAvailable;
                StatusText = $"Update available: {_remotePatch.LatestVersion}";

                // Try to get download size via HEAD request
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                        var patchUrl = $"{_updateInfoConfig.UpdateAddress}/{_remotePatch.LatestVersion}.zip";
                        var req = new HttpRequestMessage(HttpMethod.Head, patchUrl);
                        var resp = await client.SendAsync(req);
                        if (resp.Content.Headers.ContentLength is long len && len > 0)
                            DownloadSizeText = $"Download: ~{len / 1024 / 1024} MB";
                    }
                    catch { /* Size estimate not critical */ }
                });

                PatchNotes = $"🎮 ToyBattles Update\n\n" +
                             $"Current version: {installedVersion}\n" +
                             $"New version: {_remotePatch.LatestVersion}\n\n" +
                             $"Click UPDATE to download and install the latest patch.";
            }
            else
            {
                IsVersionUpToDate = true;
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
                await InstallFullGameAsync();
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

        // Disk space check — require at least 2 GB free on the game drive
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(_localState.GameRootPath)!);
            if (drive.AvailableFreeSpace < 2L * 1024 * 1024 * 1024)
            {
                State = LauncherState.Error;
                StatusText = $"Not enough disk space. At least 2 GB free is required " +
                             $"({drive.AvailableFreeSpace / 1024 / 1024} MB available).";
                return;
            }
        }
        catch { /* Skip if drive info unavailable */ }

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

                ProgressPercent = 100;
                DownloadSpeed = string.Empty;
                Eta = string.Empty;
                DownloadSizeText = string.Empty;

                ShowBalloonRequested?.Invoke(
                    "ToyBattles Updated",
                    $"Updated to {_remotePatch.LatestVersion} — ready to play!");

                // Re-check in case there is a further patch on top of the one we just applied
                StatusText = "Update applied. Verifying latest version...";
                await CheckForUpdatesAsync();

                // If still up to date, set friendly message
                if (State == LauncherState.Ready)
                {
                    StatusText = "Update complete! Ready to play.";
                    PatchNotes = $"🎮 ToyBattles\n\n" +
                                 $"Version: {_remotePatch.LatestVersion}\n\n" +
                                 $"Update applied successfully! Click PLAY to launch.";
                }
                // If another update was found, DownloadAndApplyUpdateAsync will be triggered
                // by the caller (InstallFullGameAsync) or the user can click UPDATE
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

    /// <summary>
    /// Downloads the remote cgd.dip and compares its Adler32 checksum against the local copy.
    /// Updates the local file if they differ — matches reference launcher's CheckCgdDipAsync.
    /// </summary>
    private async Task CheckCgdDipAsync()
    {
        if (string.IsNullOrEmpty(_localState.GameRootPath)) return;

        var cgdLocalPath = Path.Combine(_localState.GameRootPath, "data", "cgd.dip");
        if (!File.Exists(cgdLocalPath))
        {
            LogService.Log("cgd.dip not found locally, skipping check.");
            return;
        }

        var serverAddr = GetServerAddress();
        var cgdUrl = $"{serverAddr}/microvolts/Full/data/cgd.dip";

        LogService.Log($"Checking cgd.dip from {cgdUrl}");

        try
        {
            var remoteData = await _downloadService.DownloadBytesAsync(cgdUrl);
            if (remoteData == null)
            {
                LogService.Log("Failed to download remote cgd.dip, skipping check.");
                return;
            }

            var localData = await File.ReadAllBytesAsync(cgdLocalPath);
            var localChecksum = PatchService.Adler32(localData);
            var remoteChecksum = PatchService.Adler32(remoteData);

            LogService.Log($"cgd.dip — local: {localData.Length} bytes ({localChecksum:x8}), remote: {remoteData.Length} bytes ({remoteChecksum:x8})");

            if (localChecksum != remoteChecksum)
            {
                LogService.Log("cgd.dip checksum mismatch — updating...");
                await File.WriteAllBytesAsync(cgdLocalPath, remoteData);
                LogService.Log("cgd.dip updated successfully.");
            }
            else
            {
                LogService.Log("cgd.dip checksums match, no update needed.");
            }
        }
        catch (Exception ex)
        {
            LogService.LogError("cgd.dip check failed", ex);
        }
    }

    private async Task DoPingServerStatusAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync("http://cdn.toybattles.net/");
            var isOnline = response.IsSuccessStatusCode || (int)response.StatusCode < 500;
            ServerStatusText = isOnline ? "Online" : "Offline";
            ServerStatusBrush = isOnline
                ? new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x71))
                : new SolidColorBrush(Color.FromRgb(0xFF, 0x47, 0x57));
        }
        catch
        {
            ServerStatusText = "Offline";
            ServerStatusBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x47, 0x57));
        }
    }

    private void LaunchGame()
    {
        if (string.IsNullOrEmpty(_localState.GameRootPath))
            return;

        // Quick file integrity scan before launching
        State = LauncherState.VerifyingFiles;
        StatusText = "Verifying game files...";
        ProgressPercent = 0;

        if (!ValidateCriticalFiles(_localState.GameRootPath))
        {
            LogService.LogError($"Critical game files missing in: {_localState.GameRootPath}");
            _localState.GameRootPath = null;
            _localState.Save();
            State = LauncherState.NeedGameRoot;
            StatusText = "Game files are missing or corrupt. Click INSTALL to re-download.";
            return;
        }

        ProgressPercent = 100;
        State = LauncherState.Launching;
        StatusText = "Launching ToyBattles...";

        // Record session start before launching — persisted so it survives launcher close
        _localState.PendingSessionStartUtc = DateTime.UtcNow;
        _localState.Save();

        var proc = LaunchService.Launch(_localState.GameRootPath!, _localState.LaunchArguments);
        if (proc != null)
        {
            StatusText = "Game launched!";
            if (!_localState.KeepLauncherOpen)
            {
                // Launcher closes immediately — session start is persisted; time recovered on next open
                proc.Dispose();
                System.Windows.Application.Current.Dispatcher.BeginInvoke(
                    System.Windows.Threading.DispatcherPriority.Background,
                    () => System.Windows.Application.Current.Shutdown());
            }
            else
            {
                State = LauncherState.Ready;
                StatusText = "Game is running.";

                // Monitor game process — restore launcher when game exits
                _ = Task.Run(async () =>
                {
                    try { await proc.WaitForExitAsync(); }
                    catch { /* process may have already exited */ }
                    finally { proc.Dispose(); }

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        // Record session end accurately
                        if (_localState.PendingSessionStartUtc is { } start)
                        {
                            var elapsed = (long)(DateTime.UtcNow - start).TotalSeconds;
                            _localState.TotalPlaytimeSeconds += Math.Max(0, elapsed);
                            _localState.PendingSessionStartUtc = null;
                            _localState.Save();
                            UpdatePlaytimeText();
                        }

                        State = LauncherState.Ready;
                        StatusText = "Game session ended. Ready to play again.";
                        RestoreWindowRequested?.Invoke();
                    });
                });
            }
        }
        else
        {
            // Launch failed — clear the pending session we just saved
            _localState.PendingSessionStartUtc = null;
            _localState.Save();
            State = LauncherState.Error;
            StatusText = "Failed to launch game. Check if MicroVolts.exe exists.";
        }
    }

    public event Func<string?>? OnInstallFolderRequested;

    /// <summary>
    /// Full game installation flow — downloads the complete game from the CDN.
    /// </summary>
    private async Task InstallFullGameAsync()
    {
        // Ask the user where to install via folder picker
        var installDir = OnInstallFolderRequested?.Invoke();
        if (string.IsNullOrEmpty(installDir))
        {
            // User cancelled the folder picker
            return;
        }

        // Disk space check — require at least 5 GB free for a full install
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(installDir)!);
            if (drive.AvailableFreeSpace < 5L * 1024 * 1024 * 1024)
            {
                State = LauncherState.NeedGameRoot;
                StatusText = $"Not enough disk space. At least 5 GB free is required " +
                             $"({drive.AvailableFreeSpace / 1024 / 1024} MB available on selected drive).";
                return;
            }
        }
        catch { /* Skip if drive info unavailable */ }

        State = LauncherState.Installing;
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
            // Determine FullFileAddress
            var fullFileAddr = _updateInfoConfig?.FullFileAddress;

            if (string.IsNullOrEmpty(fullFileAddr))
            {
                // Try to fetch updateinfo.ini from alongside the launcher
                var cwd = AppDomain.CurrentDomain.BaseDirectory;
                var localUpdateInfo = Path.Combine(cwd, "updateinfo.ini");
                if (File.Exists(localUpdateInfo))
                {
                    _updateInfoConfig = UpdateInfoConfig.Load(localUpdateInfo);
                    fullFileAddr = _updateInfoConfig.FullFileAddress;
                }
            }

            if (string.IsNullOrEmpty(fullFileAddr))
            {
                LogService.LogError("No FullFileAddress configured. Cannot install.");
                State = LauncherState.Error;
                StatusText = "No download URL configured. Please place updateinfo.ini next to the launcher.";
                return;
            }

            LogService.Log($"Installing game to: {installDir}");

            StatusText = $"Installing to: {installDir}";

            var gameRoot = await _installService.InstallFullGameAsync(
                installDir, fullFileAddr, progress, _cts.Token);

            if (gameRoot != null)
            {
                _localState.GameRootPath = gameRoot;
                _localState.Save();

                // Copy updateinfo.ini into the game root so future launches can find update URLs
                try
                {
                    var launcherDir = AppDomain.CurrentDomain.BaseDirectory;
                    var srcUpdateInfo = Path.Combine(launcherDir, "updateinfo.ini");
                    if (File.Exists(srcUpdateInfo))
                    {
                        var destUpdateInfo = Path.Combine(gameRoot, "updateinfo.ini");
                        File.Copy(srcUpdateInfo, destUpdateInfo, overwrite: true);
                    }
                }
                catch (Exception ex)
                {
                    LogService.LogError("Failed to copy updateinfo.ini to game root", ex);
                }

                // Load the version from the freshly installed files
                LoadLocalVersion();

                StatusText = $"Installed to: {gameRoot}. Checking for updates...";
                ProgressPercent = 100;
                DownloadSpeed = string.Empty;
                Eta = string.Empty;

                // Check for updates — the Full.zip may be older than the latest patch
                await CheckForUpdatesAsync();

                // If a patch is available, apply it automatically — no need for user to click again
                if (State == LauncherState.UpdateAvailable)
                    await DownloadAndApplyUpdateAsync();
            }
            else
            {
                State = LauncherState.Error;
                StatusText = "Installation failed. Check logs for details.";
            }
        }
        catch (OperationCanceledException)
        {
            State = LauncherState.NeedGameRoot;
            StatusText = "Installation cancelled.";
        }
        catch (Exception ex)
        {
            LogService.LogError("Installation failed", ex);
            State = LauncherState.Error;
            StatusText = $"Installation failed: {ex.Message}";
        }
    }

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

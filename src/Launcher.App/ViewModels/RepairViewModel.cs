using System.IO;
using System.Windows.Input;
using Launcher.Core.Config;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.App.ViewModels;

/// <summary>
/// ViewModel for the Repair view — verify and repair game files, or fully reinstall.
/// </summary>
public class RepairViewModel : ViewModelBase
{
    private readonly RepairService _repairService = new();
    private readonly InstallService _installService = new();
    private CancellationTokenSource? _cts;

    private bool _isRepairing;
    public bool IsRepairing
    {
        get => _isRepairing;
        set
        {
            if (SetProperty(ref _isRepairing, value))
            {
                OnPropertyChanged(nameof(IsRepairEnabled));
                OnPropertyChanged(nameof(IsReinstallEnabled));
            }
        }
    }

    private double _progressPercent;
    public double ProgressPercent
    {
        get => _progressPercent;
        set => SetProperty(ref _progressPercent, value);
    }

    private string _statusText = "Click 'Verify & Repair' to check your game files.";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private string _currentFileText = string.Empty;
    public string CurrentFileText
    {
        get => _currentFileText;
        set => SetProperty(ref _currentFileText, value);
    }

    private string _resultSummary = string.Empty;
    public string ResultSummary
    {
        get => _resultSummary;
        set => SetProperty(ref _resultSummary, value);
    }

    private string _cacheSizeText = string.Empty;
    public string CacheSizeText
    {
        get => _cacheSizeText;
        set => SetProperty(ref _cacheSizeText, value);
    }

    public bool IsRepairEnabled => !IsRepairing;
    public bool IsReinstallEnabled => !IsRepairing;

    public ICommand RepairCommand { get; }
    public ICommand FullReinstallCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ClearCacheCommand { get; }

    // Event to request install folder from the View
    public event Func<string?>? OnInstallFolderRequested;

    public RepairViewModel()
    {
        RepairCommand = new AsyncRelayCommand(OnRepairAsync);
        FullReinstallCommand = new AsyncRelayCommand(OnFullReinstallAsync);
        CancelCommand = new RelayCommand(_ => _cts?.Cancel());
        ClearCacheCommand = new AsyncRelayCommand(OnClearCacheAsync);

        _ = RefreshCacheSizeAsync();
    }

    private async Task RefreshCacheSizeAsync()
    {
        await Task.Run(() =>
        {
            var tempDir = Path.GetTempPath();
            long totalSize = 0;

            foreach (var dir in Directory.GetDirectories(tempDir, "LauncherPatch_*"))
            {
                try
                {
                    foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                        totalSize += new FileInfo(file).Length;
                }
                catch { }
            }

            foreach (var file in Directory.GetFiles(tempDir, "ToyBattles_FullInstall_*"))
            {
                try { totalSize += new FileInfo(file).Length; } catch { }
            }

            CacheSizeText = totalSize > 0
                ? $"{DownloadService.FormatBytes(totalSize)} in temp files"
                : string.Empty;
        });
    }

    private async Task OnClearCacheAsync()
    {
        await Task.Run(() =>
        {
            var tempDir = Path.GetTempPath();
            int count = 0;

            foreach (var dir in Directory.GetDirectories(tempDir, "LauncherPatch_*"))
            {
                try { Directory.Delete(dir, recursive: true); count++; } catch { }
            }

            foreach (var file in Directory.GetFiles(tempDir, "ToyBattles_FullInstall_*"))
            {
                try { File.Delete(file); count++; } catch { }
            }

            CacheSizeText = count > 0 ? $"Cleared {count} temp item(s)." : "No temp files found.";
            LogService.Log($"Cache clear: removed {count} item(s).");
        });

        await Task.Delay(2500);
        CacheSizeText = string.Empty;
    }

    private async Task OnRepairAsync()
    {
        IsRepairing = true;
        StatusText = "Verifying game files...";
        ProgressPercent = 0;
        ResultSummary = string.Empty;
        CurrentFileText = string.Empty;
        _cts = new CancellationTokenSource();

        try
        {
            var state = LocalState.Load();
            if (string.IsNullOrEmpty(state.GameRootPath))
            {
                StatusText = "Game root not set. Please configure in Settings.";
                return;
            }

            var updateInfoPath = Path.Combine(state.GameRootPath, "updateinfo.ini");
            if (!File.Exists(updateInfoPath))
            {
                StatusText = "updateinfo.ini not found in game directory.";
                return;
            }

            var updateInfo = UpdateInfoConfig.Load(updateInfoPath);
            if (string.IsNullOrEmpty(updateInfo.FullFileAddress))
            {
                StatusText = "Full file URL not configured.";
                return;
            }

            var progress = new Progress<RepairService.RepairProgress>(p =>
            {
                ProgressPercent = p.ProgressPercent;
                StatusText = $"Checking files... ({p.CheckedFiles}/{p.TotalFiles})";
                CurrentFileText = p.CurrentFile;
            });

            var result = await _repairService.VerifyAndRepairAsync(
                state.GameRootPath,
                updateInfo.FullFileAddress,
                progress);

            CurrentFileText = string.Empty;
            ResultSummary = $"✅ Files checked: {result.CheckedFiles}\n" +
                           $"🔧 Files repaired: {result.RepairedFiles}\n" +
                           $"❌ Failures: {result.FailedFiles}";

            StatusText = result.FailedFiles > 0
                ? "Repair completed with some errors. Check logs."
                : result.RepairedFiles > 0
                    ? "Repair completed successfully!"
                    : "All files verified — no repairs needed!";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Repair cancelled.";
            CurrentFileText = string.Empty;
        }
        catch (Exception ex)
        {
            LogService.LogError("Repair failed", ex);
            StatusText = $"Repair failed: {ex.Message}";
            CurrentFileText = string.Empty;
        }
        finally
        {
            IsRepairing = false;
            ProgressPercent = IsRepairing ? ProgressPercent : 100;
        }
    }

    private async Task OnFullReinstallAsync()
    {
        var state = LocalState.Load();

        // Use existing game root as default, or ask user if not set
        var installDir = state.GameRootPath;
        if (string.IsNullOrEmpty(installDir))
        {
            installDir = OnInstallFolderRequested?.Invoke();
            if (string.IsNullOrEmpty(installDir))
                return;
        }

        // Load updateinfo.ini to find download URL
        var updateInfoPath = Path.Combine(installDir, "updateinfo.ini");
        if (!File.Exists(updateInfoPath))
        {
            StatusText = "updateinfo.ini not found. Cannot determine download URL.";
            return;
        }

        var updateInfo = UpdateInfoConfig.Load(updateInfoPath);
        if (string.IsNullOrEmpty(updateInfo.FullFileAddress))
        {
            StatusText = "Full file URL not configured in updateinfo.ini.";
            return;
        }

        IsRepairing = true;
        StatusText = "Starting full reinstall...";
        ProgressPercent = 0;
        ResultSummary = string.Empty;
        CurrentFileText = string.Empty;
        _cts = new CancellationTokenSource();

        var progress = new Progress<DownloadProgress>(p =>
        {
            ProgressPercent = p.ProgressPercent;
            StatusText = p.StatusText;
            CurrentFileText = string.Empty;
        });

        try
        {
            var gameRoot = await _installService.InstallFullGameAsync(
                installDir, updateInfo.FullFileAddress, progress, _cts.Token);

            if (gameRoot != null)
            {
                state.GameRootPath = gameRoot;
                state.Save();
                StatusText = "Reinstall complete!";
                ResultSummary = "✅ Full reinstall completed successfully.";
            }
            else
            {
                StatusText = "Reinstall failed. Check logs for details.";
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "Reinstall cancelled.";
        }
        catch (Exception ex)
        {
            LogService.LogError("Full reinstall failed", ex);
            StatusText = $"Reinstall failed: {ex.Message}";
        }
        finally
        {
            IsRepairing = false;
            ProgressPercent = 100;
            CurrentFileText = string.Empty;
        }
    }
}

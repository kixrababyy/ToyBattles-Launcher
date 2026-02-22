using System.IO;
using System.Windows.Input;
using Launcher.Core.Config;
using Launcher.Core.Models;
using Launcher.Core.Services;

namespace Launcher.App.ViewModels;

/// <summary>
/// ViewModel for the Repair view — verify and repair game files.
/// </summary>
public class RepairViewModel : ViewModelBase
{
    private readonly RepairService _repairService = new();

    private bool _isRepairing;
    public bool IsRepairing
    {
        get => _isRepairing;
        set
        {
            if (SetProperty(ref _isRepairing, value))
                OnPropertyChanged(nameof(IsRepairEnabled));
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

    private string _resultSummary = string.Empty;
    public string ResultSummary
    {
        get => _resultSummary;
        set => SetProperty(ref _resultSummary, value);
    }

    public bool IsRepairEnabled => !IsRepairing;

    public ICommand RepairCommand { get; }

    public RepairViewModel()
    {
        RepairCommand = new AsyncRelayCommand(OnRepairAsync);
    }

    private async Task OnRepairAsync()
    {
        IsRepairing = true;
        StatusText = "Verifying game files...";
        ProgressPercent = 0;
        ResultSummary = string.Empty;

        try
        {
            var state = LocalState.Load();
            if (string.IsNullOrEmpty(state.GameRootPath))
            {
                StatusText = "Game root not set. Please configure in Settings.";
                return;
            }

            // Load update info
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
                StatusText = $"Checking: {p.CurrentFile} ({p.CheckedFiles}/{p.TotalFiles})";
            });

            var result = await _repairService.VerifyAndRepairAsync(
                state.GameRootPath,
                updateInfo.FullFileAddress,
                progress);

            ResultSummary = $"✅ Files checked: {result.CheckedFiles}\n" +
                           $"🔧 Files repaired: {result.RepairedFiles}\n" +
                           $"❌ Failures: {result.FailedFiles}";

            StatusText = result.FailedFiles > 0
                ? "Repair completed with some errors. Check logs."
                : result.RepairedFiles > 0
                    ? "Repair completed successfully!"
                    : "All files verified — no repairs needed!";
        }
        catch (Exception ex)
        {
            LogService.LogError("Repair failed", ex);
            StatusText = $"Repair failed: {ex.Message}";
        }
        finally
        {
            IsRepairing = false;
            ProgressPercent = 100;
        }
    }
}

using System.Diagnostics;
using System.Windows.Input;

namespace Launcher.App.ViewModels;

/// <summary>
/// Main shell ViewModel — controls navigation between Home, Settings, Repair pages.
/// </summary>
public class MainViewModel : ViewModelBase
{
    private ViewModelBase _currentPage;
    public ViewModelBase CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value);
    }

    private string _selectedNav = "Home";
    public string SelectedNav
    {
        get => _selectedNav;
        set => SetProperty(ref _selectedNav, value);
    }

    public HomeViewModel HomeVM { get; }
    public SettingsViewModel SettingsVM { get; }
    public RepairViewModel RepairVM { get; }

    public ICommand NavigateHomeCommand { get; }
    public ICommand NavigateSettingsCommand { get; }
    public ICommand NavigateRepairCommand { get; }
    public ICommand NavigateNewsCommand { get; }
    public ICommand NavigateDiscordCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand MinimizeCommand { get; }

    public MainViewModel()
    {
        HomeVM = new HomeViewModel();
        SettingsVM = new SettingsViewModel();
        RepairVM = new RepairViewModel();
        _currentPage = HomeVM;

        NavigateHomeCommand = new RelayCommand(_ => { CurrentPage = HomeVM; SelectedNav = "Home"; });
        NavigateSettingsCommand = new RelayCommand(_ => { CurrentPage = SettingsVM; SelectedNav = "Settings"; SettingsVM.Refresh(); });
        NavigateRepairCommand = new RelayCommand(_ => { CurrentPage = RepairVM; SelectedNav = "Repair"; });

        NavigateNewsCommand = new RelayCommand(_ =>
            OpenUrl("https://toybattles.net/clubhouse#news"));
        NavigateDiscordCommand = new RelayCommand(_ =>
            OpenUrl("https://dc.toybattles.net"));

        CloseCommand = new RelayCommand(_ =>
            System.Windows.Application.Current.Shutdown());
        MinimizeCommand = new RelayCommand(_ =>
        {
            var window = System.Windows.Application.Current.MainWindow;
            if (window != null)
                window.WindowState = System.Windows.WindowState.Minimized;
        });
    }

    public async Task InitializeAsync()
    {
        await HomeVM.InitializeAsync();
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { /* Swallow if browser fails to open */ }
    }
}

using System.Diagnostics;
using System.Windows.Input;
using Launcher.Core.Models;

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

    private string _selectedNav = "Leaderboard";
    public string SelectedNav
    {
        get => _selectedNav;
        set => SetProperty(ref _selectedNav, value);
    }

    private bool _isBannerMode;
    public bool IsBannerMode
    {
        get => _isBannerMode;
        set => SetProperty(ref _isBannerMode, value);
    }

    public HomeViewModel HomeVM { get; }
    public SettingsViewModel SettingsVM { get; }
    public RepairViewModel RepairVM { get; }
    public LeaderboardViewModel LeaderboardVM { get; }

    public ICommand NavigateHomeCommand { get; }
    public ICommand NavigateLeaderboardCommand { get; }
    public ICommand ShowBannerCommand { get; }
    public ICommand NavigateSettingsCommand { get; }
    public ICommand NavigateRepairCommand { get; }
    public ICommand NavigateNewsCommand { get; }
    public ICommand NavigateDiscordCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand MinimizeCommand { get; }

    public System.Collections.ObjectModel.ObservableCollection<Launcher.Core.Models.CustomClient> CustomClients { get; } = new();
    public ICommand AddClientCommand { get; }
    public ICommand SelectClientCommand { get; }
    public ICommand RemoveClientCommand { get; }

    public MainViewModel()
    {
        HomeVM = new HomeViewModel();
        SettingsVM = new SettingsViewModel();
        RepairVM = new RepairViewModel();
        LeaderboardVM = new LeaderboardViewModel();
        _currentPage = LeaderboardVM;

        NavigateHomeCommand = new RelayCommand(_ => SelectCustomClient(null));
        NavigateLeaderboardCommand = new RelayCommand(_ => { CurrentPage = LeaderboardVM; SelectedNav = "Leaderboard"; IsBannerMode = false; });
        ShowBannerCommand = new RelayCommand(_ => { CurrentPage = HomeVM; SelectedNav = "Banner"; IsBannerMode = true; });
        NavigateSettingsCommand = new RelayCommand(_ => { CurrentPage = SettingsVM; SelectedNav = "Settings"; IsBannerMode = false; SettingsVM.Refresh(); });
        NavigateRepairCommand = new RelayCommand(_ => { CurrentPage = RepairVM; SelectedNav = "Repair"; IsBannerMode = false; });

        NavigateNewsCommand = new RelayCommand(_ =>
            OpenUrl("https://toybattles.net/"));
        NavigateDiscordCommand = new RelayCommand(_ =>
            OpenUrl("https://discord.gg/toybattles"));

        CloseCommand = new RelayCommand(_ =>
            System.Windows.Application.Current.Shutdown());
        MinimizeCommand = new RelayCommand(_ =>
        {
            var window = System.Windows.Application.Current.MainWindow;
            if (window != null)
                window.WindowState = System.Windows.WindowState.Minimized;
        });

        // Load custom clients from state
        var state = LocalState.Load();
        foreach (var client in state.CustomClients)
        {
            CustomClients.Add(client);
        }

        AddClientCommand = new RelayCommand(_ => AddCustomClient());
        SelectClientCommand = new RelayCommand(param => SelectCustomClient(param as CustomClient));
        RemoveClientCommand = new RelayCommand(param => RemoveCustomClient(param as CustomClient));
    }

    private void AddCustomClient()
    {
        var folderDialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Game Client Folder"
        };
        
        if (folderDialog.ShowDialog() == true)
        {
            var fileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Client Icon",
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.ico)|*.png;*.jpg;*.jpeg;*.ico|All files (*.*)|*.*"
            };

            if (fileDialog.ShowDialog() == true)
            {
                var newClient = new CustomClient
                {
                    Name = new System.IO.DirectoryInfo(folderDialog.FolderName).Name,
                    Path = folderDialog.FolderName,
                    IconPath = fileDialog.FileName
                };

                CustomClients.Add(newClient);
                SaveClientsToState();
                SelectCustomClient(newClient);
            }
        }
    }

    private void SelectCustomClient(CustomClient? client)
    {
        CurrentPage = HomeVM;
        IsBannerMode = false;

        if (client == null)
        {
            SelectedNav = "Home";
        }
        else
        {
            SelectedNav = client.Id;
        }

        HomeVM.SetActiveClient(client);
    }

    private void RemoveCustomClient(CustomClient? client)
    {
        if (client != null && CustomClients.Contains(client))
        {
            CustomClients.Remove(client);
            SaveClientsToState();

            if (SelectedNav == client.Id)
            {
                SelectCustomClient(null); // Revert to home
            }
        }
    }

    private void SaveClientsToState()
    {
        var state = LocalState.Load();
        state.CustomClients = new System.Collections.Generic.List<CustomClient>(CustomClients);
        state.Save();
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

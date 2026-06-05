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

    private string _selectedNav = "Home";
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

    public ICommand NavigateHomeCommand { get; }
    public ICommand ShowBannerCommand { get; }
    public ICommand NavigateSettingsCommand { get; }
    public ICommand NavigateRepairCommand { get; }
    public ICommand NavigateNewsCommand { get; }
    public ICommand NavigateDiscordCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand MinimizeCommand { get; }

    public System.Collections.ObjectModel.ObservableCollection<Launcher.Core.Models.CustomClient> CustomClients { get; } = new();
    private bool _isAddClientOpen;
    public bool IsAddClientOpen
    {
        get => _isAddClientOpen;
        set => SetProperty(ref _isAddClientOpen, value);
    }

    private string _newClientName = string.Empty;
    public string NewClientName
    {
        get => _newClientName;
        set => SetProperty(ref _newClientName, value);
    }

    private string _newClientPath = string.Empty;
    public string NewClientPath
    {
        get => _newClientPath;
        set => SetProperty(ref _newClientPath, value);
    }

    private string _newClientIconPath = string.Empty;
    public string NewClientIconPath
    {
        get => _newClientIconPath;
        set => SetProperty(ref _newClientIconPath, value);
    }

    public ICommand AddClientCommand { get; }
    public ICommand CloseAddClientCommand { get; }
    public ICommand BrowseClientPathCommand { get; }
    public ICommand BrowseClientIconCommand { get; }
    public ICommand ConfirmAddClientCommand { get; }

    public ICommand SelectClientCommand { get; }
    public ICommand RemoveClientCommand { get; }

    public MainViewModel()
    {
        HomeVM = new HomeViewModel();
        SettingsVM = new SettingsViewModel();
        RepairVM = new RepairViewModel();
        _currentPage = HomeVM;
        NavigateHomeCommand = new RelayCommand(_ => SelectCustomClient(null));
        ShowBannerCommand = new RelayCommand(_ => { CurrentPage = HomeVM; SelectedNav = "Banner"; IsBannerMode = true; HomeVM.RefreshStateFromDisk(); });
        NavigateSettingsCommand = new RelayCommand(_ => { CurrentPage = SettingsVM; SelectedNav = "Settings"; IsBannerMode = false; SettingsVM.Refresh(); });
        NavigateRepairCommand = new RelayCommand(_ => { CurrentPage = RepairVM; SelectedNav = "Repair"; IsBannerMode = false; });

        NavigateNewsCommand = new RelayCommand(_ =>
            OpenUrl("https://toybattles.net/"));
        NavigateDiscordCommand = new RelayCommand(_ =>
            OpenUrl("https://discord.gg/toybattles"));

        CloseCommand = new RelayCommand(_ => System.Windows.Application.Current.Shutdown());
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

        AddClientCommand = new RelayCommand(_ => 
        {
            NewClientName = string.Empty;
            NewClientPath = string.Empty;
            NewClientIconPath = string.Empty;
            IsAddClientOpen = true;
        });

        CloseAddClientCommand = new RelayCommand(_ => IsAddClientOpen = false);

        BrowseClientPathCommand = new RelayCommand(_ =>
        {
            var folderDialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Game Client Folder"
            };
            if (folderDialog.ShowDialog() == true)
            {
                NewClientPath = folderDialog.FolderName;
                if (string.IsNullOrWhiteSpace(NewClientName))
                {
                    NewClientName = new System.IO.DirectoryInfo(folderDialog.FolderName).Name;
                }
            }
        });

        BrowseClientIconCommand = new RelayCommand(_ =>
        {
            var fileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select Client Icon",
                Filter = "Image Files (*.png;*.jpg;*.jpeg;*.ico)|*.png;*.jpg;*.jpeg;*.ico|All files (*.*)|*.*"
            };
            if (fileDialog.ShowDialog() == true)
            {
                NewClientIconPath = fileDialog.FileName;
            }
        });

        ConfirmAddClientCommand = new RelayCommand(_ =>
        {
            if (string.IsNullOrWhiteSpace(NewClientName) || string.IsNullOrWhiteSpace(NewClientPath))
                return;

            var newClient = new CustomClient
            {
                Name = NewClientName,
                Path = NewClientPath,
                IconPath = string.IsNullOrWhiteSpace(NewClientIconPath) ? null : NewClientIconPath
            };

            CustomClients.Add(newClient);
            SaveClientsToState();
            SelectCustomClient(newClient);
            IsAddClientOpen = false;
        });

        SelectClientCommand = new RelayCommand(param => SelectCustomClient(param as CustomClient));
        RemoveClientCommand = new RelayCommand(param => RemoveCustomClient(param as CustomClient));
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

        HomeVM.RefreshStateFromDisk();
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
        
        // Restore the selected custom client (or Home) to keep the UI sidebar and bindings in sync
        var state = Launcher.Core.Models.LocalState.Load();
        if (!string.IsNullOrEmpty(state.ActiveClientId))
        {
            var activeClient = CustomClients.FirstOrDefault(c => c.Id == state.ActiveClientId);
            SelectCustomClient(activeClient); // Will fallback to Home if activeClient is null
        }
        else
        {
            SelectCustomClient(null);
        }
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

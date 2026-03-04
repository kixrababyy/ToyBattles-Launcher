using System.Windows;
using Launcher.App.Views;
using Launcher.Core.Services;

namespace Launcher.App;

public partial class App : Application
{
    private async void App_Startup(object sender, StartupEventArgs e)
    {
#if VALENTINE_THEME
        Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Themes/Valentine.xaml", UriKind.Absolute)
        });
#endif

        var splash = new SplashWindow();
        splash.Show();

        var applyUpdate = false;

        try
        {
            var (needsUpdate, remoteVersion) = await LauncherUpdateService.CheckAsync();

            if (needsUpdate && remoteVersion != null)
            {
                // Prevent infinite restart loops if the downloaded exe doesn't bump its AssemblyVersion
                var localState = Launcher.Core.Models.LocalState.Load();
                var remoteStr = remoteVersion.ToString(3);
                
                if (localState.LastAttemptedUpdateVersion == remoteStr)
                {
                    LogService.Log($"Launcher already attempted to update to {remoteStr} but failed. Skipping auto-update to prevent boot loop.");
                }
                else
                {
                    var label = $"v{remoteVersion.Major}.{remoteVersion.Minor}.{remoteVersion.Build}";
                    splash.SetStatus($"Downloading launcher update {label}...");

                    var progress = new Progress<DownloadProgress>(p => splash.SetDownloadProgress(p));

                    localState.LastAttemptedUpdateVersion = remoteStr;
                    localState.Save();

                    await LauncherUpdateService.DownloadAndApplyAsync(progress);

                    splash.SetStatus("Restarting...");
                    await Task.Delay(600);
                    applyUpdate = true;
                }
            }
        }
        catch (Exception ex)
        {
            // Non-fatal — log and fall through to normal startup
            LogService.LogError("Launcher self-update failed, continuing with current version", ex);
        }

        if (applyUpdate)
        {
            // Bat script has been launched; exit so it can replace the exe
            splash.Close();
            Shutdown();
            return;
        }

        // Normal launch — no update available (or update failed gracefully)
        splash.Close();
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}

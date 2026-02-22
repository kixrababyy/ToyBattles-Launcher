using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Launcher.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void SettingsView_Loaded(object sender, RoutedEventArgs e)
    {
        var sb = (Storyboard)FindResource("FadeInStoryboard");
        sb.Begin();
    }
}

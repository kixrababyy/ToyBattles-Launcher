using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Launcher.App.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }

    private void HomeView_Loaded(object sender, RoutedEventArgs e)
    {
        // Trigger fade-in + slide-up animation
        var sb = (Storyboard)FindResource("FadeInStoryboard");
        sb.Begin();
    }
}

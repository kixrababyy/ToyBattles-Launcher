using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Launcher.App.Views;

public partial class RepairView : UserControl
{
    public RepairView()
    {
        InitializeComponent();
    }

    private void RepairView_Loaded(object sender, RoutedEventArgs e)
    {
        var sb = (Storyboard)FindResource("FadeInStoryboard");
        sb.Begin();
    }
}

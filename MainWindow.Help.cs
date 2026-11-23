using System.Windows;

namespace DevClean;

public partial class MainWindow
{
    private void HelpButton_Click(object sender, RoutedEventArgs e) =>
        HelpMenu.Show((FrameworkElement)sender, this);
}

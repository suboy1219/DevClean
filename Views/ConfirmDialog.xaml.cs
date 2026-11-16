using System.Windows;
using System.Windows.Input;
using DevClean.Models;
using DevClean.Utilities;

namespace DevClean.Views;

public partial class ConfirmDialog : Window
{
    public ConfirmDialog(IReadOnlyList<CleanItem> items, long totalBytes)
    {
        InitializeComponent();

        ItemList.ItemsSource = items;
        SummaryText.Text = $"即将清理 {items.Count} 项，预计释放 {ByteFormat.Size(totalBytes)}";

        bool hasDanger = items.Any(i => i.Risk == RiskLevel.Danger);
        if (hasDanger)
        {
            DangerCheck.Visibility = Visibility.Visible;
            ConfirmButton.IsEnabled = false;
        }

        CancelButton.Click += (_, _) => DialogResult = false;
        ConfirmButton.Click += (_, _) => DialogResult = true;
    }

    private void OnDangerCheckChanged(object sender, RoutedEventArgs e)
    {
        ConfirmButton.IsEnabled = DangerCheck.IsChecked == true;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            DialogResult = false;
        }
    }
}

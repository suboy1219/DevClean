using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DevClean.Models;
using DevClean.ViewModels;

namespace DevClean;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(bool demo = false)
    {
        InitializeComponent();
        _viewModel = new MainViewModel(demo);
        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += OnLoaded;
        Closed += (_, _) => _viewModel.Cancel();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await _viewModel.InitializeAsync();
    }

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemList.SelectedItem is CleanItem item)
        {
            _viewModel.OpenItem(item);
        }
    }

    private void OnItemCheckBoxClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.CheckBox { DataContext: CleanItem item } checkBox)
        {
            item.Selected = checkBox.IsChecked == true;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsScanning) && !_viewModel.IsScanning)
        {
            Dispatcher.BeginInvoke(ScrollListToTop, DispatcherPriority.Background);
        }
    }

    private void ScrollListToTop()
    {
        FindVisualChild<System.Windows.Controls.ScrollViewer>(ItemList)?.ScrollToTop();
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                return match;
            }

            T? nested = FindVisualChild<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}

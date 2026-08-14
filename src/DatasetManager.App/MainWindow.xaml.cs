using System.Windows;
using System.Windows.Controls.Primitives;
using DatasetManager.App.ViewModels;
using DatasetManager.Core.Models;

namespace DatasetManager.App;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void All_Click(object sender, RoutedEventArgs e) => SelectNavigation(null, AllNavigation);
    private void Raw_Click(object sender, RoutedEventArgs e) => SelectNavigation(DatasetType.Raw, RawNavigation);
    private void Processed_Click(object sender, RoutedEventArgs e) => SelectNavigation(DatasetType.Processed, ProcessedNavigation);
    private void Test_Click(object sender, RoutedEventArgs e) => SelectNavigation(DatasetType.Test, TestNavigation);
    private void Validation_Click(object sender, RoutedEventArgs e) => SelectNavigation(DatasetType.Validation, ValidationNavigation);

    private void SelectNavigation(DatasetType? type, ToggleButton selected)
    {
        AllNavigation.IsChecked = false;
        RawNavigation.IsChecked = false;
        ProcessedNavigation.IsChecked = false;
        TestNavigation.IsChecked = false;
        ValidationNavigation.IsChecked = false;
        selected.IsChecked = true;
        ViewModel.SetFilter(type);
    }
}

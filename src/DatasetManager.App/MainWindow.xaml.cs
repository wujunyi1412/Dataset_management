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
    private void Created_Click(object sender, RoutedEventArgs e) => SelectNavigation(DatasetType.Created, CreatedNavigation);
    private void Training_Click(object sender, RoutedEventArgs e) => SelectNavigation(DatasetType.Training, TrainingNavigation);
    private void Test_Click(object sender, RoutedEventArgs e) => SelectNavigation(DatasetType.Test, TestNavigation);
    private void Validation_Click(object sender, RoutedEventArgs e) => SelectNavigation(DatasetType.Validation, ValidationNavigation);
    private void Yolo_Click(object sender, RoutedEventArgs e)
    {
        ClearNavigationSelection();
        YoloNavigation.IsChecked = true;
        ViewModel.SetYoloModule();
    }
    private void Weight_Click(object sender, RoutedEventArgs e)
    {
        ClearNavigationSelection();
        WeightNavigation.IsChecked = true;
        ViewModel.SetWeightModule();
    }

    private void SelectNavigation(DatasetType? type, ToggleButton selected)
    {
        ClearNavigationSelection();
        selected.IsChecked = true;
        ViewModel.SetFilter(type);
    }

    private void ClearNavigationSelection()
    {
        AllNavigation.IsChecked = false;
        RawNavigation.IsChecked = false;
        ProcessedNavigation.IsChecked = false;
        CreatedNavigation.IsChecked = false;
        TrainingNavigation.IsChecked = false;
        TestNavigation.IsChecked = false;
        ValidationNavigation.IsChecked = false;
        YoloNavigation.IsChecked = false;
        WeightNavigation.IsChecked = false;
    }
}

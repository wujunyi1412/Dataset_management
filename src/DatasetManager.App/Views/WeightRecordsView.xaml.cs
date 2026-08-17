using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DatasetManager.Core.Models;
using DatasetManager.Core.Services;

namespace DatasetManager.App.Views;

public partial class WeightRecordsView : UserControl, INotifyPropertyChanged
{
    private readonly WeightRecordRepository _repository = new();
    private WeightRecord? _selectedWeight;
    private string _searchText = string.Empty;
    private bool _loaded;

    public WeightRecordsView()
    {
        InitializeComponent();
        DataContext = this;
        WeightsView = CollectionViewSource.GetDefaultView(Weights);
        WeightsView.Filter = FilterWeight;
        Loaded += OnLoaded;
    }

    public ObservableCollection<WeightRecord> Weights { get; } = [];
    public ICollectionView WeightsView { get; }
    public bool HasWeights => Weights.Count > 0;

    public WeightRecord? SelectedWeight
    {
        get => _selectedWeight;
        set { if (_selectedWeight == value) return; _selectedWeight = value; OnPropertyChanged(); }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged();
            WeightsView.Refresh();
            if (SelectedWeight is not null && !FilterWeight(SelectedWeight))
                SelectedWeight = Weights.Cast<WeightRecord>().FirstOrDefault(FilterWeight);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            foreach (var record in (await _repository.LoadAsync()).OrderByDescending(x => x.UpdatedAt)) Weights.Add(record);
            SelectedWeight = Weights.FirstOrDefault();
            OnPropertyChanged(nameof(HasWeights));
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "无法载入权重记录", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new WeightEditorWindow(null) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;
        dialog.Result.ChangeHistory.Add(new WeightChangeEntry { Description = "创建权重记录" });
        Weights.Insert(0, dialog.Result);
        SelectedWeight = dialog.Result;
        OnPropertyChanged(nameof(HasWeights));
        await SaveAsync();
    }

    private async void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedWeight is null) return;
        var record = SelectedWeight;
        var dialog = new WeightEditorWindow(record) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;
        record.Name = dialog.Result.Name;
        record.FilePath = dialog.Result.FilePath;
        record.Notes = dialog.Result.Notes;
        record.UpdatedAt = DateTimeOffset.Now;
        record.ChangeHistory.Add(new WeightChangeEntry { Description = string.IsNullOrWhiteSpace(dialog.ChangeDescription) ? "更新权重信息" : dialog.ChangeDescription });
        WeightsView.Refresh();
        SelectedWeight = null;
        SelectedWeight = record;
        await SaveAsync();
    }

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedWeight is null) return;
        var record = SelectedWeight;
        if (MessageBox.Show($"移除权重记录“{record.Name}”？\n\n只删除管理记录，不会删除权重文件。", "确认移除", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        Weights.Remove(record);
        SelectedWeight = Weights.Cast<WeightRecord>().FirstOrDefault(FilterWeight);
        OnPropertyChanged(nameof(HasWeights));
        await SaveAsync();
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedWeight is null || !File.Exists(SelectedWeight.FilePath))
        {
            MessageBox.Show("权重文件不存在。", "无法打开", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{SelectedWeight.FilePath}\"") { UseShellExecute = true });
    }

    private bool FilterWeight(object item)
    {
        if (item is not WeightRecord record) return false;
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        return record.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
               || record.FilePath.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
               || record.Format.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
               || record.Notes.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    private async Task SaveAsync()
    {
        try { await _repository.SaveAsync(Weights); }
        catch (Exception exception) { MessageBox.Show(exception.Message, "保存权重记录失败", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

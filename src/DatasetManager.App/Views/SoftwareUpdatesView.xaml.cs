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
using Microsoft.Win32;

namespace DatasetManager.App.Views;

public partial class SoftwareUpdatesView : UserControl, INotifyPropertyChanged
{
    private readonly SoftwareUpdateRepository _repository = new();
    private readonly SoftwareUpdateScanner _scanner = new();
    private SoftwareUpdateRecord? _selectedRecord;
    private SoftwareUpdateScan? _selectedScan;
    private string _searchText = string.Empty;
    private string _activityText = string.Empty;
    private bool _isBusy;
    private bool _loaded;

    public SoftwareUpdatesView()
    {
        InitializeComponent();
        DataContext = this;
        RecordsView = CollectionViewSource.GetDefaultView(Records);
        RecordsView.Filter = FilterRecord;
        Loaded += OnLoaded;
    }

    public ObservableCollection<SoftwareUpdateRecord> Records { get; } = [];
    public ICollectionView RecordsView { get; }
    public bool HasRecords => Records.Count > 0;
    public bool IsNotBusy => !_isBusy;

    public SoftwareUpdateRecord? SelectedRecord
    {
        get => _selectedRecord;
        set
        {
            if (_selectedRecord == value) return;
            _selectedRecord = value;
            SelectedScan = value?.ScanHistory.OrderByDescending(x => x.ScannedAt).FirstOrDefault();
            OnPropertyChanged();
        }
    }

    public SoftwareUpdateScan? SelectedScan
    {
        get => _selectedScan;
        set { if (_selectedScan == value) return; _selectedScan = value; OnPropertyChanged(); }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged();
            RecordsView.Refresh();
            if (SelectedRecord is not null && !FilterRecord(SelectedRecord))
                SelectedRecord = Records.Cast<SoftwareUpdateRecord>().FirstOrDefault(FilterRecord);
        }
    }

    public string ActivityText
    {
        get => _activityText;
        private set { if (_activityText == value) return; _activityText = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            foreach (var record in (await _repository.LoadAsync()).OrderByDescending(x => x.LastScannedAt ?? x.CreatedAt))
            {
                record.ScanHistory = record.ScanHistory.OrderByDescending(x => x.ScannedAt).ToList();
                Records.Add(record);
            }
            SelectedRecord = Records.FirstOrDefault();
            OnPropertyChanged(nameof(HasRecords));
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "无法载入软件更新记录", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void Add_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        var dialog = new OpenFolderDialog { Title = "选择要跟踪更新的软件文件夹", Multiselect = false };
        if (dialog.ShowDialog() != true) return;

        var fullPath = Path.GetFullPath(dialog.FolderName);
        var existing = Records.FirstOrDefault(x => string.Equals(
            Path.GetFullPath(x.FolderPath), fullPath, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SelectedRecord = existing;
            MessageBox.Show("这个文件夹已经在更新记录中。", "无需重复添加", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var record = new SoftwareUpdateRecord
        {
            Name = new DirectoryInfo(fullPath).Name,
            FolderPath = fullPath
        };
        Records.Insert(0, record);
        SelectedRecord = record;
        OnPropertyChanged(nameof(HasRecords));
        if (!await ScanAsync(record, true))
        {
            Records.Remove(record);
            SelectedRecord = Records.FirstOrDefault();
            OnPropertyChanged(nameof(HasRecords));
        }
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRecord is not null) await ScanAsync(SelectedRecord, false);
    }

    private async void SaveNotes_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRecord is null || _isBusy) return;
        try
        {
            await _repository.SaveAsync(Records);
            RecordsView.Refresh();
            ActivityText = "文件夹备注已保存";
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "保存文件夹备注失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task<bool> ScanAsync(SoftwareUpdateRecord record, bool isBaseline)
    {
        if (_isBusy) return false;
        SetBusy(true);
        ActivityText = "正在扫描…";
        try
        {
            var result = await _scanner.ScanAsync(record.FolderPath, isBaseline
                ? new Dictionary<string, SoftwareFileSnapshot>(StringComparer.OrdinalIgnoreCase)
                : record.Snapshot);
            var scan = new SoftwareUpdateScan
            {
                ScannedAt = DateTimeOffset.Now,
                IsBaseline = isBaseline,
                TotalFileCount = result.Snapshot.Count,
                AddedFiles = isBaseline ? [] : result.AddedFiles,
                ModifiedFiles = isBaseline ? [] : result.ModifiedFiles,
                DeletedFiles = isBaseline ? [] : result.DeletedFiles
            };
            record.Snapshot = result.Snapshot;
            record.LastScannedAt = scan.ScannedAt;
            record.ScanHistory.Insert(0, scan);
            SelectedScan = scan;
            RecordsView.Refresh();
            await _repository.SaveAsync(Records);
            ActivityText = scan.Summary;
            return true;
        }
        catch (Exception exception)
        {
            ActivityText = "扫描失败";
            MessageBox.Show(exception.Message, "无法扫描软件文件夹", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRecord is null || !Directory.Exists(SelectedRecord.FolderPath))
        {
            MessageBox.Show("软件文件夹不存在。", "无法打开", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{SelectedRecord.FolderPath}\"") { UseShellExecute = true });
    }

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRecord is null || _isBusy) return;
        var record = SelectedRecord;
        if (MessageBox.Show($"移除“{record.Name}”的更新记录？\n\n只会删除扫描记录，不会删除文件夹中的任何文件。",
                "确认移除", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        Records.Remove(record);
        SelectedRecord = Records.Cast<SoftwareUpdateRecord>().FirstOrDefault(FilterRecord);
        OnPropertyChanged(nameof(HasRecords));
        await _repository.SaveAsync(Records);
    }

    private bool FilterRecord(object item) => item is SoftwareUpdateRecord record
        && (string.IsNullOrWhiteSpace(SearchText)
            || record.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || record.FolderPath.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || record.Notes.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

    private void SetBusy(bool value)
    {
        _isBusy = value;
        OnPropertyChanged(nameof(IsNotBusy));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Data;
using DatasetManager.App.Views;
using DatasetManager.Core.Models;
using DatasetManager.Core.Services;
using DatasetManager.Native;

namespace DatasetManager.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IDatasetRepository _repository = new JsonDatasetRepository();
    private readonly DatasetScanner _scanner = new(new NativeImageService());
    private readonly LabelMeAnnotationScanner _annotationScanner = new();
    private readonly CompositeDatasetService _compositeDatasetService = new();
    private readonly MaterializedDatasetService _materializedDatasetService = new();
    private readonly List<DatasetRecord> _models = [];
    private DatasetItemViewModel? _selectedDataset;
    private AnnotationSetRecord? _selectedAnnotationSet;
    private DatasetType? _filterType;
    private string _searchText = string.Empty;
    private string _statusText = "正在载入…";
    private bool _isYoloModule;
    private bool _isWeightModule;

    public MainViewModel()
    {
        DatasetsView = CollectionViewSource.GetDefaultView(Datasets);
        DatasetsView.Filter = FilterDataset;
        AddCommand = new RelayCommand(AddDataset);
        EditCommand = new RelayCommand(EditDataset, () => SelectedDataset?.Model.Type is DatasetType.Raw or DatasetType.Processed);
        RemoveCommand = new AsyncRelayCommand(RemoveDatasetAsync, () => SelectedDataset is not null);
        RefreshCommand = new AsyncRelayCommand(RefreshSelectedAsync, () => SelectedDataset is not null && SelectedDataset.IsImageDataset);
        OpenFolderCommand = new RelayCommand(OpenSelectedFolder, () => SelectedDataset is not null);
        AddAnnotationCommand = new RelayCommand(AddAnnotationSet, CanAddAnnotationSet);
        EditAnnotationCommand = new RelayCommand(EditAnnotationSet, () => SelectedAnnotationSet is not null);
        RemoveAnnotationCommand = new AsyncRelayCommand(RemoveAnnotationSetAsync, () => SelectedAnnotationSet is not null);
        RefreshAnnotationCommand = new AsyncRelayCommand(RefreshAnnotationSetAsync, () => SelectedAnnotationSet is not null);
        _ = LoadAsync();
    }

    public ObservableCollection<DatasetItemViewModel> Datasets { get; } = [];
    public ICollectionView DatasetsView { get; }
    public RelayCommand AddCommand { get; }
    public RelayCommand EditCommand { get; }
    public AsyncRelayCommand RemoveCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand AddAnnotationCommand { get; }
    public RelayCommand EditAnnotationCommand { get; }
    public AsyncRelayCommand RemoveAnnotationCommand { get; }
    public AsyncRelayCommand RefreshAnnotationCommand { get; }
    public string NativeStatus { get; } = new NativeImageService().GetNativeVersion();
    public bool IsYoloModule => _isYoloModule;
    public bool IsWeightModule => _isWeightModule;
    public bool IsDatasetModule => !_isYoloModule && !_isWeightModule;

    public DatasetItemViewModel? SelectedDataset
    {
        get => _selectedDataset;
        set
        {
            if (!SetProperty(ref _selectedDataset, value)) return;
            EditCommand.RaiseCanExecuteChanged();
            RemoveCommand.RaiseCanExecuteChanged();
            RefreshCommand.RaiseCanExecuteChanged();
            OpenFolderCommand.RaiseCanExecuteChanged();
            SelectedAnnotationSet = value?.AnnotationSets.FirstOrDefault();
            AddAnnotationCommand.RaiseCanExecuteChanged();
        }
    }

    public AnnotationSetRecord? SelectedAnnotationSet
    {
        get => _selectedAnnotationSet;
        set
        {
            if (!SetProperty(ref _selectedAnnotationSet, value)) return;
            EditAnnotationCommand.RaiseCanExecuteChanged();
            RemoveAnnotationCommand.RaiseCanExecuteChanged();
            RefreshAnnotationCommand.RaiseCanExecuteChanged();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value)) return;
            DatasetsView.Refresh();
            EnsureSelectionMatchesCurrentView();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public int RawCount => _models.Count(x => x.Type == DatasetType.Raw);
    public int ProcessedCount => _models.Count(x => x.Type == DatasetType.Processed);
    public int CreatedCount => _models.Count(x => x.Type == DatasetType.Created);
    public int TrainingCount => _models.Count(x => x.Type == DatasetType.Training);
    public int TestCount => _models.Count(x => x.Type == DatasetType.Test);
    public int ValidationCount => _models.Count(x => x.Type == DatasetType.Validation);
    public string PageTitle => _filterType switch
    {
        DatasetType.Training => "训练集",
        DatasetType.Test => "测试集",
        DatasetType.Validation => "验证集",
        DatasetType.Raw => "原始数据集",
        DatasetType.Processed => "已处理数据集",
        DatasetType.Created => "创建数据集",
        _ => "数据集目录"
    };
    public string PageSubtitle => _filterType switch
    {
        DatasetType.Created => "从数据集清单复制图片与标签，形成独立数据集",
        DatasetType.Training or DatasetType.Test or DatasetType.Validation => "创建数据集 · 后续可扩展标签转换与重复检查",
        _ => "管理来源、用途和每一次修改"
    };
    public string AddButtonText => _filterType switch
    {
        DatasetType.Created => "＋ 复制创建",
        DatasetType.Training or DatasetType.Test or DatasetType.Validation => "＋ 创建数据集",
        _ => "＋ 添加数据集"
    };

    public void SetFilter(DatasetType? type)
    {
        _isYoloModule = false;
        _isWeightModule = false;
        RaisePropertyChanged(nameof(IsYoloModule));
        RaisePropertyChanged(nameof(IsWeightModule));
        RaisePropertyChanged(nameof(IsDatasetModule));
        _filterType = type;
        DatasetsView.Refresh();
        EnsureSelectionMatchesCurrentView();
        StatusText = type switch
        {
            DatasetType.Raw => "原始数据集",
            DatasetType.Processed => "已处理数据集",
            DatasetType.Created => "创建数据集",
            DatasetType.Training => "训练集",
            DatasetType.Test => "测试集",
            DatasetType.Validation => "验证集",
            _ => "全部数据集"
        };
        RaisePropertyChanged(nameof(PageTitle));
        RaisePropertyChanged(nameof(PageSubtitle));
        RaisePropertyChanged(nameof(AddButtonText));
    }

    public void SetYoloModule()
    {
        _isYoloModule = true;
        _isWeightModule = false;
        RaisePropertyChanged(nameof(IsYoloModule));
        RaisePropertyChanged(nameof(IsWeightModule));
        RaisePropertyChanged(nameof(IsDatasetModule));
        StatusText = "YOLO处理";
    }

    public void SetWeightModule()
    {
        _isYoloModule = false;
        _isWeightModule = true;
        RaisePropertyChanged(nameof(IsYoloModule));
        RaisePropertyChanged(nameof(IsWeightModule));
        RaisePropertyChanged(nameof(IsDatasetModule));
        StatusText = "权重记录";
    }

    private async Task LoadAsync()
    {
        try
        {
            _models.AddRange(await _repository.LoadAsync());
            RebuildItems();
            StatusText = $"已载入 {_models.Count} 个数据集";
        }
        catch (Exception exception)
        {
            StatusText = "载入失败";
            MessageBox.Show(exception.Message, "无法载入目录库", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void AddDataset()
    {
        if (_filterType == DatasetType.Created)
        {
            _ = CreateMaterializedDatasetAsync();
            return;
        }
        if (_filterType is DatasetType.Training or DatasetType.Test or DatasetType.Validation)
        {
            _ = CreateCompositeDatasetAsync(_filterType.Value);
            return;
        }
        var dialog = new DatasetEditorWindow(_models, null) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        dialog.Result.ChangeHistory.Add(new DatasetChangeEntry
        {
            Description = string.IsNullOrWhiteSpace(dialog.ChangeDescription) ? "创建数据集记录" : dialog.ChangeDescription
        });
        _models.Add(dialog.Result);
        RebuildItems(dialog.Result.Id);
        _ = SaveAndScanAsync(dialog.Result);
    }

    private async Task CreateMaterializedDatasetAsync()
    {
        var dialog = new MaterializedDatasetCreatorWindow(_models) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var progress = new Progress<DatasetCopyProgress>(x =>
                StatusText = $"正在复制文件：{x.Completed:N0} / {x.Total:N0} 对");
            var record = await _materializedDatasetService.CreateAsync(
                dialog.DatasetName,
                dialog.ManifestPath,
                dialog.DestinationParent,
                dialog.ImagesFolderName,
                dialog.LabelsFolderName,
                dialog.Notes,
                progress);
            _models.Add(record);
            RebuildItems(record.Id);
            await SaveAsync();
            StatusText = $"已复制创建 {record.Name}，共 {record.Materialization?.PairCount ?? 0:N0} 对文件";
        }
        catch (Exception exception)
        {
            StatusText = "复制创建失败";
            MessageBox.Show(exception.Message, "复制创建数据集失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task CreateCompositeDatasetAsync(DatasetType type)
    {
        var dialog = new CompositeDatasetCreatorWindow(type, _models) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true) return;
        if (File.Exists(dialog.ManifestPath)
            && MessageBox.Show($"清单文件已存在：\n{dialog.ManifestPath}\n\n是否覆盖？", "确认覆盖",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;

        try
        {
            StatusText = $"正在创建{PageTitle}…";
            var record = await _compositeDatasetService.CreateAsync(
                dialog.DatasetName,
                type,
                dialog.ManifestPath,
                dialog.Notes,
                dialog.Sources.Select(x => x.ToRequest()).ToArray());
            _models.Add(record);
            RebuildItems(record.Id);
            await SaveAsync();
            StatusText = $"已创建 {record.Name}，共 {record.Composition?.PairCount ?? 0} 对数据";
        }
        catch (Exception exception)
        {
            StatusText = "创建数据集失败";
            MessageBox.Show(exception.Message, "创建数据集失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void EditDataset()
    {
        if (SelectedDataset is null) return;
        var model = SelectedDataset.Model;
        var dialog = new DatasetEditorWindow(_models, model) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        model.Name = dialog.Result.Name;
        model.Type = dialog.Result.Type;
        model.RootPath = dialog.Result.RootPath;
        model.ParentDatasetId = dialog.Result.ParentDatasetId;
        model.Notes = dialog.Result.Notes;
        model.UpdatedAt = DateTimeOffset.Now;
        model.ChangeHistory.Add(new DatasetChangeEntry
        {
            Description = string.IsNullOrWhiteSpace(dialog.ChangeDescription) ? "更新数据集信息" : dialog.ChangeDescription
        });
        RebuildItems(model.Id);
        _ = SaveAndScanAsync(model);
    }

    private async Task RemoveDatasetAsync()
    {
        if (SelectedDataset is null) return;
        var model = SelectedDataset.Model;
        var children = _models.Count(x => x.ParentDatasetId == model.Id);
        var deletesFiles = model.Type == DatasetType.Created && model.Materialization?.OwnsRootDirectory == true;
        var suffix = children > 0 ? $"\n\n有 {children} 个派生数据集会解除源数据集关联。" : string.Empty;
        var message = deletesFiles
            ? $"将永久删除“{model.Name}”的实际数据集目录和管理记录：\n\n{model.RootPath}\n\n来源文件和来源清单不会删除。此操作不可恢复。"
            : $"只删除“{model.Name}”的管理记录，不会删除磁盘文件。{suffix}";
        if (MessageBox.Show(message, deletesFiles ? "确认删除实际数据集" : "确认移除",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;

        if (deletesFiles)
        {
            try
            {
                StatusText = $"正在删除 {model.Name}…";
                await Task.Run(() => _materializedDatasetService.DeleteOwnedDataset(model));
            }
            catch (Exception exception)
            {
                StatusText = $"删除 {model.Name} 失败";
                MessageBox.Show(exception.Message, "删除数据集失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        foreach (var child in _models.Where(x => x.ParentDatasetId == model.Id)) child.ParentDatasetId = null;
        _models.Remove(model);
        RebuildItems();
        await SaveAsync();
        StatusText = deletesFiles
            ? $"已删除 {model.Name} 的实际数据集和管理记录"
            : $"已移除 {model.Name} 的管理记录";
    }

    private async Task RefreshSelectedAsync()
    {
        if (SelectedDataset is not null) await SaveAndScanAsync(SelectedDataset.Model);
    }

    private bool CanAddAnnotationSet() => SelectedDataset?.Model.Type == DatasetType.Processed;

    private void AddAnnotationSet()
    {
        if (!CanAddAnnotationSet() || SelectedDataset is null) return;
        var dialog = new AnnotationEditorWindow(null) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        var model = SelectedDataset.Model;
        model.AnnotationSets ??= [];
        model.AnnotationSets.Add(dialog.Result);
        model.UpdatedAt = DateTimeOffset.Now;
        model.ChangeHistory.Add(new DatasetChangeEntry { Description = $"添加标注批次：{dialog.Result.Name}" });
        SelectedAnnotationSet = dialog.Result;
        RebuildItems(model.Id);
        SelectedAnnotationSet = dialog.Result;
        _ = ScanAndSaveAnnotationAsync(model, dialog.Result);
    }

    private void EditAnnotationSet()
    {
        if (SelectedDataset is null || SelectedAnnotationSet is null) return;
        var annotation = SelectedAnnotationSet;
        var dialog = new AnnotationEditorWindow(annotation) { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true || dialog.Result is null) return;

        annotation.Name = dialog.Result.Name;
        annotation.LabelPath = dialog.Result.LabelPath;
        annotation.Notes = dialog.Result.Notes;
        annotation.UpdatedAt = DateTimeOffset.Now;
        var model = SelectedDataset.Model;
        model.UpdatedAt = DateTimeOffset.Now;
        model.ChangeHistory.Add(new DatasetChangeEntry { Description = $"更新标注批次：{annotation.Name}" });
        RebuildItems(model.Id);
        SelectedAnnotationSet = annotation;
        _ = ScanAndSaveAnnotationAsync(model, annotation);
    }

    private async Task RemoveAnnotationSetAsync()
    {
        if (SelectedDataset is null || SelectedAnnotationSet is null) return;
        var annotation = SelectedAnnotationSet;
        if (MessageBox.Show($"移除标注批次“{annotation.Name}”？只删除管理记录，不删除标签文件。", "确认移除",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;

        var model = SelectedDataset.Model;
        model.AnnotationSets.Remove(annotation);
        model.UpdatedAt = DateTimeOffset.Now;
        model.ChangeHistory.Add(new DatasetChangeEntry { Description = $"移除标注批次：{annotation.Name}" });
        SelectedAnnotationSet = null;
        RebuildItems(model.Id);
        await SaveAsync();
    }

    private async Task RefreshAnnotationSetAsync()
    {
        if (SelectedDataset is not null && SelectedAnnotationSet is not null)
            await ScanAndSaveAnnotationAsync(SelectedDataset.Model, SelectedAnnotationSet);
    }

    private async Task ScanAndSaveAnnotationAsync(DatasetRecord dataset, AnnotationSetRecord annotation)
    {
        StatusText = $"正在读取标注批次 {annotation.Name}…";
        annotation.Statistics = await _annotationScanner.ScanAsync(annotation.LabelPath, dataset.RootPath);
        annotation.UpdatedAt = DateTimeOffset.Now;
        await SaveAsync();
        RebuildItems(dataset.Id);
        SelectedAnnotationSet = annotation;
        StatusText = annotation.Statistics.ScanError ??
            $"{annotation.Name}：{annotation.Statistics.JsonFileCount} 个 JSON，{annotation.Statistics.AnnotationCount} 个标注实例，{annotation.Statistics.ClassCounts.Count} 类";
    }

    private void OpenSelectedFolder()
    {
        var rootPath = SelectedDataset?.RootPath;
        if (rootPath is null || (!Directory.Exists(rootPath) && !File.Exists(rootPath)))
        {
            MessageBox.Show("数据集目录不存在。", "无法打开", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var arguments = File.Exists(rootPath) ? $"/select,\"{rootPath}\"" : $"\"{rootPath}\"";
        Process.Start(new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = true });
    }

    private async Task SaveAndScanAsync(DatasetRecord model)
    {
        StatusText = $"正在扫描 {model.Name}…";
        model.Statistics = await _scanner.ScanAsync(model.RootPath);
        var item = Datasets.FirstOrDefault(x => x.Id == model.Id);
        item?.RefreshAll();
        await SaveAsync();
        StatusText = model.Statistics.ScanError is null
            ? $"{model.Name}：{model.Statistics.ImageCount} 张图片"
            : $"{model.Name}：{model.Statistics.ScanError}";
    }

    private async Task SaveAsync()
    {
        try { await _repository.SaveAsync(_models); }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool FilterDataset(object item)
    {
        if (item is not DatasetItemViewModel dataset) return false;
        if (_filterType is not null && dataset.Model.Type != _filterType) return false;
        if (string.IsNullOrWhiteSpace(SearchText)) return true;
        return dataset.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
               || dataset.RootPath.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
               || dataset.Notes.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    private string ResolveParentName(Guid? id) =>
        id is null ? "—" : _models.FirstOrDefault(x => x.Id == id)?.Name ?? "来源已移除";

    private void RebuildItems(Guid? selectedId = null)
    {
        selectedId ??= SelectedDataset?.Id;
        Datasets.Clear();
        foreach (var model in _models.OrderByDescending(x => x.UpdatedAt))
            Datasets.Add(new DatasetItemViewModel(model, ResolveParentName));
        SelectedDataset = Datasets.FirstOrDefault(x => x.Id == selectedId && FilterDataset(x))
            ?? Datasets.FirstOrDefault(x => FilterDataset(x));
        DatasetsView.Refresh();
        RaisePropertyChanged(nameof(RawCount));
        RaisePropertyChanged(nameof(ProcessedCount));
        RaisePropertyChanged(nameof(CreatedCount));
        RaisePropertyChanged(nameof(TrainingCount));
        RaisePropertyChanged(nameof(TestCount));
        RaisePropertyChanged(nameof(ValidationCount));
    }

    private void EnsureSelectionMatchesCurrentView()
    {
        if (SelectedDataset is not null && FilterDataset(SelectedDataset)) return;
        SelectedDataset = Datasets.FirstOrDefault(x => FilterDataset(x));
    }
}

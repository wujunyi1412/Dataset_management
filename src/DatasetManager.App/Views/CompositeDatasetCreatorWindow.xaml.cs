using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using DatasetManager.Core.Models;
using DatasetManager.Core.Services;
using Microsoft.Win32;

namespace DatasetManager.App.Views;

public partial class CompositeDatasetCreatorWindow : Window
{
    private readonly DatasetType _type;
    private readonly ImageLabelPairFinder _pairFinder = new();
    private CancellationTokenSource? _pairCountCancellation;
    private int _availablePairCount = -1;

    public CompositeDatasetCreatorWindow(DatasetType type, IReadOnlyCollection<DatasetRecord> datasets)
    {
        InitializeComponent();
        DataContext = this;
        _type = type;
        HeadingText.Text = $"创建{GetTypeName(type)}";
        DatasetBox.ItemsSource = datasets
            .Where(x => x.Type == DatasetType.Processed && (x.AnnotationSets?.Count ?? 0) > 0)
            .OrderBy(x => x.Name)
            .ToArray();
        DatasetBox.SelectedIndex = DatasetBox.Items.Count > 0 ? 0 : -1;
    }

    public ObservableCollection<CompositeSourceDraft> Sources { get; } = [];
    public string DatasetName => NameBox.Text.Trim();
    public string Notes => NotesBox.Text.Trim();
    public string ManifestPath
    {
        get
        {
            var invalid = Path.GetInvalidFileNameChars();
            var safeName = new string(DatasetName.Select(x => invalid.Contains(x) ? '_' : x).ToArray());
            return Path.Combine(OutputBox.Text.Trim(), $"{safeName}.dataset.json");
        }
    }

    private void DatasetBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        AnnotationBox.ItemsSource = (DatasetBox.SelectedItem as DatasetRecord)?.AnnotationSets ?? [];
        AnnotationBox.SelectedIndex = AnnotationBox.Items.Count > 0 ? 0 : -1;
    }

    private async void AnnotationBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _pairCountCancellation?.Cancel();
        _pairCountCancellation?.Dispose();
        _pairCountCancellation = new CancellationTokenSource();
        var cancellationToken = _pairCountCancellation.Token;
        _availablePairCount = -1;
        AddSourceButton.IsEnabled = false;
        SourceErrorText.Text = string.Empty;

        if (DatasetBox.SelectedItem is not DatasetRecord dataset
            || AnnotationBox.SelectedItem is not AnnotationSetRecord annotation)
        {
            AvailableCountText.Text = " / 请选择标注";
            AnnotationNotesText.Text = "标注备注：请选择标注批次";
            return;
        }

        AnnotationNotesText.Text = string.IsNullOrWhiteSpace(annotation.Notes)
            ? "标注备注：暂无备注"
            : $"标注备注：{annotation.Notes}";
        AvailableCountText.Text = " / 正在统计…";
        try
        {
            var count = await Task.Run(
                () => _pairFinder.Find(dataset.RootPath, annotation.LabelPath).Count,
                cancellationToken);
            if (cancellationToken.IsCancellationRequested) return;
            _availablePairCount = count;
            AddSourceButton.IsEnabled = count > 0;
            UpdateAvailableCountText();
            ValidateCountInput();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (cancellationToken.IsCancellationRequested) return;
            AvailableCountText.Text = " / 统计失败";
            SourceErrorText.Text = exception.Message;
        }
    }

    private void UseAll_Changed(object sender, RoutedEventArgs e)
    {
        if (CountBox is null) return;
        CountBox.IsEnabled = UseAllCheck.IsChecked != true;
        UpdateAvailableCountText();
        ValidateCountInput();
    }

    private void CountBox_TextChanged(object sender, TextChangedEventArgs e) => ValidateCountInput();

    private void UpdateAvailableCountText()
    {
        if (AvailableCountText is null || _availablePairCount < 0) return;
        AvailableCountText.Text = UseAllCheck.IsChecked == true
            ? $" / 共 {_availablePairCount} 对"
            : $" / 最多 {_availablePairCount} 对";
    }

    private bool ValidateCountInput()
    {
        if (SourceErrorText is null) return true;
        if (UseAllCheck?.IsChecked == true)
        {
            SourceErrorText.Text = string.Empty;
            return true;
        }
        if (_availablePairCount < 0)
        {
            SourceErrorText.Text = "正在统计可用匹配对，请稍候。";
            return false;
        }
        if (!int.TryParse(CountBox.Text, out var count) || count <= 0)
        {
            SourceErrorText.Text = "选取数量必须是大于 0 的整数。";
            return false;
        }
        if (count > _availablePairCount)
        {
            SourceErrorText.Text = $"最多只能选取 {_availablePairCount} 对，当前填写了 {count} 对。";
            return false;
        }
        SourceErrorText.Text = string.Empty;
        return true;
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择数据集清单保存目录", Multiselect = false };
        if (Directory.Exists(OutputBox.Text)) dialog.InitialDirectory = OutputBox.Text;
        if (dialog.ShowDialog(this) == true) OutputBox.Text = dialog.FolderName;
    }

    private void AddSource_Click(object sender, RoutedEventArgs e)
    {
        SourceErrorText.Text = string.Empty;
        if (DatasetBox.SelectedItem is not DatasetRecord dataset || AnnotationBox.SelectedItem is not AnnotationSetRecord annotation)
        {
            SourceErrorText.Text = "请选择一个有标注批次的已处理数据集。";
            return;
        }
        if (Sources.Any(x => x.Dataset.Id == dataset.Id && x.Annotation.Id == annotation.Id))
        {
            SourceErrorText.Text = "这个数据集和标注批次已经加入。";
            return;
        }
        if (_availablePairCount < 0)
        {
            SourceErrorText.Text = "可用匹配对尚未统计完成。";
            return;
        }
        if (_availablePairCount == 0)
        {
            SourceErrorText.Text = "这个来源没有可用的图片与标签匹配对。";
            return;
        }

        int? requestedCount = null;
        if (UseAllCheck.IsChecked != true)
        {
            if (!ValidateCountInput()) return;
            var count = int.Parse(CountBox.Text);
            requestedCount = count;
        }
        Sources.Add(new CompositeSourceDraft(dataset, annotation, requestedCount, _availablePairCount));
    }

    private void RemoveSource_Click(object sender, RoutedEventArgs e)
    {
        if (SourcesList.SelectedItem is CompositeSourceDraft source) Sources.Remove(source);
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        if (string.IsNullOrWhiteSpace(DatasetName)) ErrorText.Text = "请输入数据集名称。";
        else if (!Directory.Exists(OutputBox.Text.Trim())) ErrorText.Text = "请选择一个存在的清单保存目录。";
        else if (Sources.Count == 0) ErrorText.Text = "请至少加入一个组成来源。";
        else DialogResult = true;
    }

    private static string GetTypeName(DatasetType type) => type switch
    {
        DatasetType.Training => "训练集",
        DatasetType.Test => "测试集",
        DatasetType.Validation => "验证集",
        _ => "数据集"
    };
}

public sealed record CompositeSourceDraft(
    DatasetRecord Dataset,
    AnnotationSetRecord Annotation,
    int? RequestedCount,
    int AvailableCount)
{
    public string CountSummary => RequestedCount is null
        ? $"全部 {AvailableCount} 对"
        : $"{RequestedCount} / {AvailableCount} 对";
    public string AnnotationNotes => string.IsNullOrWhiteSpace(Annotation.Notes)
        ? "标注备注：暂无备注"
        : $"标注备注：{Annotation.Notes}";
    public CompositeSourceRequest ToRequest() => new(Dataset, Annotation, RequestedCount);
}

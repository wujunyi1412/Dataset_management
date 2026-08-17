using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DatasetManager.Core.Models;
using DatasetManager.Core.Services;
using Microsoft.Win32;

namespace DatasetManager.App.Views;

public partial class YoloProcessingView : UserControl
{
    private readonly LabelMeToYoloConverter _converter = new();
    private readonly YoloDuplicateService _duplicateService = new();
    private readonly YoloOperationRepository _repository = new();
    private YoloDuplicateResult? _lastDuplicateResult;

    public YoloProcessingView()
    {
        InitializeComponent();
        DataContext = this;
        _ = LoadRecordsAsync();
    }

    public ObservableCollection<YoloOperationRecord> OperationRecords { get; } = [];

    private async Task LoadRecordsAsync()
    {
        foreach (var record in (await _repository.LoadAsync()).OrderByDescending(x => x.CreatedAt))
            OperationRecords.Add(record);
        UpdateRecordsEmptyState();
    }

    private void BrowseConversionSource_Click(object sender, RoutedEventArgs e) => PickFolder(ConversionSourceBox, "选择 LabelMe JSON 目录");
    private void BrowseConversionOutput_Click(object sender, RoutedEventArgs e) => PickFolder(ConversionOutputBox, "选择 YOLO TXT 输出目录");
    private void BrowseFolderA_Click(object sender, RoutedEventArgs e) => PickFolder(FolderABox, "选择 YOLO TXT 目录 A");
    private void BrowseFolderB_Click(object sender, RoutedEventArgs e) => PickFolder(FolderBBox, "选择 YOLO TXT 目录 B");

    private void PickFolder(TextBox target, string title)
    {
        var dialog = new OpenFolderDialog { Title = title, Multiselect = false };
        if (Directory.Exists(target.Text)) dialog.InitialDirectory = target.Text;
        if (dialog.ShowDialog(Window.GetWindow(this)) == true) target.Text = dialog.FolderName;
    }

    private async void Convert_Click(object sender, RoutedEventArgs e)
    {
        var source = ConversionSourceBox.Text.Trim();
        var output = ConversionOutputBox.Text.Trim();
        var categories = CategoriesBox.Text.Split(['\r', '\n', ',', ';', '，', '；'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal).ToArray();
        if (!Directory.Exists(source) || !Directory.Exists(output) || categories.Length == 0)
        {
            ConversionStatusText.Foreground = Brushes.Firebrick;
            ConversionStatusText.Text = "请选择存在的输入、输出目录，并至少输入一个类别。";
            return;
        }
        if (Directory.EnumerateFiles(output, "*.txt", SearchOption.AllDirectories).Any()
            && MessageBox.Show("输出目录中已有 TXT，转换可能覆盖同相对路径文件。是否继续？", "确认覆盖",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;

        ConvertButton.IsEnabled = false;
        ConversionStatusText.Foreground = Brushes.RoyalBlue;
        try
        {
            var progress = new Progress<YoloConversionProgress>(x => ConversionStatusText.Text = $"正在转换：{x.Completed} / {x.Total}");
            var result = await _converter.ConvertAsync(source, output, categories, progress);
            var categorySummary = string.Join("，", result.CategoryCounts.Select(x => $"{x.Key} {x.Value}"));
            var summary = $"{result.OutputFileCount} 个 TXT，{result.ConvertedAnnotationCount} 个标注，异常 {result.InvalidFileCount} 个；{categorySummary}";
            ConversionStatusText.Text = "转换完成：" + summary;
            await AddRecordAsync(new YoloOperationRecord
            {
                Type = YoloOperationType.FormatConversion,
                Title = "LabelMe → YOLO 格式转换",
                Summary = summary,
                Notes = ConversionNotesBox.Text.Trim(),
                SourcePathA = source,
                OutputPath = output
            });
        }
        catch (Exception exception)
        {
            ConversionStatusText.Foreground = Brushes.Firebrick;
            ConversionStatusText.Text = exception.Message;
        }
        finally { ConvertButton.IsEnabled = true; }
    }

    private void CheckDuplicates_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _lastDuplicateResult = _duplicateService.Check(FolderABox.Text.Trim(), FolderBBox.Text.Trim());
            DuplicateResultText.Text = $"目录 A：{_lastDuplicateResult.FileCountA} 个 TXT　目录 B：{_lastDuplicateResult.FileCountB} 个 TXT　重复文件名：{_lastDuplicateResult.DuplicateNameCount} 个";
            DuplicateNamesBox.Text = string.Join(Environment.NewLine, _lastDuplicateResult.DuplicateNames);
            DuplicateStatusText.Text = "检查完成，请选择处理方式并保存记录。";
        }
        catch (Exception exception)
        {
            _lastDuplicateResult = null;
            DuplicateResultText.Text = string.Empty;
            DuplicateNamesBox.Text = string.Empty;
            DuplicateStatusText.Foreground = Brushes.Firebrick;
            DuplicateStatusText.Text = exception.Message;
        }
    }

    private async void ExecuteDuplicateAction_Click(object sender, RoutedEventArgs e)
    {
        if (_lastDuplicateResult is null)
        {
            DuplicateStatusText.Foreground = Brushes.Firebrick;
            DuplicateStatusText.Text = "请先执行重复检查。";
            return;
        }
        var target = Enum.Parse<DuplicateDeleteTarget>(((ComboBoxItem)DeleteTargetBox.SelectedItem).Tag.ToString()!);
        if (target != DuplicateDeleteTarget.None)
        {
            var targetName = target == DuplicateDeleteTarget.FolderA ? "目录 A" : "目录 B";
            if (MessageBox.Show($"将永久删除{targetName}中命中的重复 TXT 文件，共涉及 {_lastDuplicateResult.DuplicateNameCount} 个重复文件名。\n\n不会删除图片、目录或非 TXT 文件。是否继续？",
                    "确认删除重复标签", MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK) return;
        }

        try
        {
            var deleted = await Task.Run(() => _duplicateService.DeleteDuplicates(_lastDuplicateResult, target));
            var action = target switch
            {
                DuplicateDeleteTarget.FolderA => $"已从目录 A 删除 {deleted} 个 TXT",
                DuplicateDeleteTarget.FolderB => $"已从目录 B 删除 {deleted} 个 TXT",
                _ => "未删除文件"
            };
            var summary = $"发现 {_lastDuplicateResult.DuplicateNameCount} 个重复文件名；{action}";
            DuplicateStatusText.Foreground = Brushes.RoyalBlue;
            DuplicateStatusText.Text = summary;
            await AddRecordAsync(new YoloOperationRecord
            {
                Type = YoloOperationType.DuplicateCheck,
                Title = "YOLO 重复数据集检查",
                Summary = summary,
                Notes = DuplicateNotesBox.Text.Trim(),
                SourcePathA = _lastDuplicateResult.FolderA,
                SourcePathB = _lastDuplicateResult.FolderB
            });
            _lastDuplicateResult = null;
        }
        catch (Exception exception)
        {
            DuplicateStatusText.Foreground = Brushes.Firebrick;
            DuplicateStatusText.Text = exception.Message;
        }
    }

    private async Task AddRecordAsync(YoloOperationRecord record)
    {
        OperationRecords.Insert(0, record);
        await _repository.SaveAsync(OperationRecords);
        UpdateRecordsEmptyState();
    }

    private void UpdateRecordsEmptyState()
    {
        EmptyRecordsText.Visibility = OperationRecords.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        OperationRecordsList.Visibility = OperationRecords.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }
}

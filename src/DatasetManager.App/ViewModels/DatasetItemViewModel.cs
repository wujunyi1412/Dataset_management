using DatasetManager.Core.Models;

namespace DatasetManager.App.ViewModels;

public sealed class DatasetItemViewModel(DatasetRecord model, Func<Guid?, string> parentNameResolver) : ObservableObject
{
    public DatasetRecord Model { get; } = model;
    public Guid Id => Model.Id;
    public string Name => Model.Name;
    public string RootPath => Model.RootPath;
    public string PathTitle => IsCompositeDataset ? "数据集清单" : "目录";
    public string Notes => Model.Notes;
    public string TypeName => Model.Type switch
    {
        DatasetType.Raw => "原始数据集",
        DatasetType.Processed => "已处理数据集",
        DatasetType.Training => "训练集",
        DatasetType.Test => "测试集",
        DatasetType.Validation => "验证集",
        _ => Model.Type.ToString()
    };
    public string ParentName => Model.ParentDatasetId is null ? "—" : parentNameResolver(Model.ParentDatasetId);
    public string ImageSummary => IsCompositeDataset
        ? $"{Model.Composition?.PairCount ?? 0:N0} 对数据"
        : $"{Model.Statistics.ImageCount:N0} 张图片";
    public bool IsCompositeDataset => Model.Type is DatasetType.Training or DatasetType.Test or DatasetType.Validation;
    public bool IsImageDataset => !IsCompositeDataset;
    public IReadOnlyList<CompositeSourceInfo> CompositionSources => Model.Composition?.Sources ?? [];
    public string CompositionSummary => $"{CompositionSources.Count:N0} 个组成来源";
    public bool CanHaveAnnotations => Model.Type == DatasetType.Processed;
    public IReadOnlyList<AnnotationSetRecord> AnnotationSets => Model.AnnotationSets ?? [];
    public string AnnotationSummary => CanHaveAnnotations ? $"{AnnotationSets.Count:N0} 组标注" : string.Empty;
    public string FolderSummary =>
        $"{Model.Statistics.SubfolderCount:N0} 个子文件夹，{Model.Statistics.ImageFolders?.Count ?? 0:N0} 个目录含图片";
    public string ResolutionSummary => "分辨率：" + JoinOrUnknown(Model.Statistics.Resolutions);
    public string BitDepthSummary => "位深：" + JoinOrUnknown(Model.Statistics.BitDepths?.Select(x => $"{x} bit"));
    public string ChannelSummary => "通道数：" + JoinOrUnknown(Model.Statistics.ChannelCounts?.Select(x => x.ToString()));
    public string UnreadableSummary => $"无法读取属性：{Model.Statistics.UnreadableImageCount:N0} 张";
    public IReadOnlyList<FolderImageStatistics> ImageFolders => Model.Statistics.ImageFolders ?? [];
    public string ScanStatus => Model.Statistics.ScanError ??
        (Model.Statistics.ScannedAt is null ? "尚未扫描" : $"扫描于 {Model.Statistics.ScannedAt:yyyy-MM-dd HH:mm}");

    public void RefreshAll()
    {
        RaisePropertyChanged(nameof(Name));
        RaisePropertyChanged(nameof(RootPath));
        RaisePropertyChanged(nameof(Notes));
        RaisePropertyChanged(nameof(TypeName));
        RaisePropertyChanged(nameof(ParentName));
        RaisePropertyChanged(nameof(ImageSummary));
        RaisePropertyChanged(nameof(IsCompositeDataset));
        RaisePropertyChanged(nameof(IsImageDataset));
        RaisePropertyChanged(nameof(CompositionSources));
        RaisePropertyChanged(nameof(CompositionSummary));
        RaisePropertyChanged(nameof(PathTitle));
        RaisePropertyChanged(nameof(CanHaveAnnotations));
        RaisePropertyChanged(nameof(AnnotationSets));
        RaisePropertyChanged(nameof(AnnotationSummary));
        RaisePropertyChanged(nameof(FolderSummary));
        RaisePropertyChanged(nameof(ResolutionSummary));
        RaisePropertyChanged(nameof(BitDepthSummary));
        RaisePropertyChanged(nameof(ChannelSummary));
        RaisePropertyChanged(nameof(UnreadableSummary));
        RaisePropertyChanged(nameof(ImageFolders));
        RaisePropertyChanged(nameof(ScanStatus));
    }

    private static string JoinOrUnknown<T>(IEnumerable<T>? values)
    {
        var items = values?.ToArray() ?? [];
        return items.Length == 0 ? "未知" : string.Join("、", items);
    }
}

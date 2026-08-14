using System.IO;
using System.Windows;
using DatasetManager.Core.Models;
using Microsoft.Win32;

namespace DatasetManager.App.Views;

public partial class DatasetEditorWindow : Window
{
    private readonly DatasetRecord? _existing;

    public DatasetEditorWindow(IReadOnlyCollection<DatasetRecord> datasets, DatasetRecord? existing)
    {
        InitializeComponent();
        _existing = existing;
        TypeBox.ItemsSource = new[]
        {
            new TypeOption("原始数据集", DatasetType.Raw),
            new TypeOption("已处理数据集", DatasetType.Processed),
            new TypeOption("测试集", DatasetType.Test),
            new TypeOption("验证集", DatasetType.Validation)
        };
        ParentBox.ItemsSource = new[] { new ParentOption("无（独立数据集）", null) }
            .Concat(datasets.Where(x => x.Id != existing?.Id && x.Type == DatasetType.Raw)
                .Select(x => new ParentOption(x.Name, x.Id)))
            .ToArray();

        if (existing is null)
        {
            TypeBox.SelectedValue = DatasetType.Raw;
            ParentBox.SelectedIndex = 0;
            return;
        }

        Title = "编辑数据集";
        NameBox.Text = existing.Name;
        TypeBox.SelectedValue = existing.Type;
        PathBox.Text = existing.RootPath;
        ParentBox.SelectedValue = existing.ParentDatasetId;
        NotesBox.Text = existing.Notes;
    }

    public DatasetRecord? Result { get; private set; }
    public string ChangeDescription => ChangeBox.Text.Trim();

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择数据集目录", Multiselect = false };
        if (Directory.Exists(PathBox.Text)) dialog.InitialDirectory = PathBox.Text;
        if (dialog.ShowDialog(this) == true) PathBox.Text = dialog.FolderName;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ErrorText.Text = "请输入数据集名称。";
            return;
        }
        if (!Directory.Exists(PathBox.Text.Trim()))
        {
            ErrorText.Text = "请选择一个存在的数据集目录。";
            return;
        }

        var selectedType = (DatasetType)(TypeBox.SelectedValue ?? DatasetType.Raw);
        var parentId = ParentBox.SelectedValue as Guid?;
        if (selectedType == DatasetType.Raw) parentId = null;

        Result = new DatasetRecord
        {
            Id = _existing?.Id ?? Guid.NewGuid(),
            Name = NameBox.Text.Trim(),
            Type = selectedType,
            RootPath = Path.GetFullPath(PathBox.Text.Trim()),
            ParentDatasetId = parentId,
            Notes = NotesBox.Text.Trim(),
            CreatedAt = _existing?.CreatedAt ?? DateTimeOffset.Now,
            UpdatedAt = DateTimeOffset.Now
        };
        DialogResult = true;
    }

    private sealed record TypeOption(string Name, DatasetType Value);
    private sealed record ParentOption(string Name, Guid? Id);
}

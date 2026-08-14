using System.IO;
using System.Windows;
using DatasetManager.Core.Models;
using Microsoft.Win32;

namespace DatasetManager.App.Views;

public partial class AnnotationEditorWindow : Window
{
    private readonly AnnotationSetRecord? _existing;

    public AnnotationEditorWindow(AnnotationSetRecord? existing)
    {
        InitializeComponent();
        _existing = existing;
        if (existing is null) return;
        Title = "编辑标注批次";
        NameBox.Text = existing.Name;
        PathBox.Text = existing.LabelPath;
        NotesBox.Text = existing.Notes;
    }

    public AnnotationSetRecord? Result { get; private set; }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择 LabelMe 标签目录", Multiselect = false };
        if (Directory.Exists(PathBox.Text)) dialog.InitialDirectory = PathBox.Text;
        if (dialog.ShowDialog(this) == true) PathBox.Text = dialog.FolderName;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ErrorText.Text = "请输入标注批次名称。";
            return;
        }
        if (!Directory.Exists(PathBox.Text.Trim()))
        {
            ErrorText.Text = "请选择一个存在的标签目录。";
            return;
        }

        Result = new AnnotationSetRecord
        {
            Id = _existing?.Id ?? Guid.NewGuid(),
            Name = NameBox.Text.Trim(),
            LabelPath = Path.GetFullPath(PathBox.Text.Trim()),
            Notes = NotesBox.Text.Trim(),
            CreatedAt = _existing?.CreatedAt ?? DateTimeOffset.Now,
            UpdatedAt = DateTimeOffset.Now,
            Statistics = _existing?.Statistics ?? new AnnotationStatistics()
        };
        DialogResult = true;
    }
}

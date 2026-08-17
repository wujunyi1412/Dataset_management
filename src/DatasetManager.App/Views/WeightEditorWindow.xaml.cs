using System.IO;
using System.Windows;
using System.Windows.Controls;
using DatasetManager.Core.Models;
using Microsoft.Win32;

namespace DatasetManager.App.Views;

public partial class WeightEditorWindow : Window
{
    private readonly WeightRecord? _existing;

    public WeightEditorWindow(WeightRecord? existing)
    {
        InitializeComponent();
        _existing = existing;
        ChangePanel.Visibility = existing is null ? Visibility.Collapsed : Visibility.Visible;
        if (existing is null) return;
        Title = "编辑权重记录";
        NameBox.Text = existing.Name;
        PathBox.Text = existing.FilePath;
        NotesBox.Text = existing.Notes;
    }

    public WeightRecord? Result { get; private set; }
    public string ChangeDescription => ChangeBox.Text.Trim();

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择权重文件",
            Filter = "常用权重文件|*.pt;*.pth;*.onnx;*.weights;*.engine;*.bin;*.ckpt|所有文件|*.*",
            CheckFileExists = true
        };
        if (File.Exists(PathBox.Text)) dialog.InitialDirectory = Path.GetDirectoryName(PathBox.Text);
        if (dialog.ShowDialog(this) != true) return;
        PathBox.Text = dialog.FileName;
        if (string.IsNullOrWhiteSpace(NameBox.Text)) NameBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
    }

    private void PathBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var extension = Path.GetExtension(PathBox.Text).TrimStart('.');
        FormatText.Text = string.IsNullOrWhiteSpace(extension) ? "格式：尚未识别" : $"格式：{extension.ToUpperInvariant()}";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text)) { ErrorText.Text = "请输入权重名称。"; return; }
        if (!File.Exists(PathBox.Text.Trim())) { ErrorText.Text = "请选择一个存在的权重文件。"; return; }

        Result = new WeightRecord
        {
            Id = _existing?.Id ?? Guid.NewGuid(),
            Name = NameBox.Text.Trim(),
            FilePath = Path.GetFullPath(PathBox.Text.Trim()),
            Notes = NotesBox.Text.Trim(),
            CreatedAt = _existing?.CreatedAt ?? DateTimeOffset.Now,
            UpdatedAt = DateTimeOffset.Now,
            ChangeHistory = _existing?.ChangeHistory ?? []
        };
        DialogResult = true;
    }
}

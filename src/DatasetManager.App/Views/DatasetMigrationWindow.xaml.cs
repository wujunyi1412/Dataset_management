using System.IO;
using System.Windows;
using DatasetManager.Core.Models;
using DatasetManager.Core.Services;
using Microsoft.Win32;

namespace DatasetManager.App.Views;

public partial class DatasetMigrationWindow : Window
{
    private readonly DatasetRecord _record;

    public DatasetMigrationWindow(DatasetRecord record)
    {
        _record = record;
        InitializeComponent();
        SourcePathText.Text = record.RootPath;
        if (Directory.Exists(record.RootPath))
        {
            var parent = Directory.GetParent(Path.GetFullPath(record.RootPath))?.FullName;
            if (parent is not null)
                DestinationBox.Text = Path.Combine(parent, $"{Path.GetFileName(Path.TrimEndingDirectorySeparator(record.RootPath))}_migrated");
        }
    }

    public string DestinationRoot => DestinationBox.Text.Trim();
    public DatasetMigrationMode Mode => MoveOption.IsChecked == true ? DatasetMigrationMode.Move : DatasetMigrationMode.Copy;

    private void BrowseDestination_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择新路径的父目录", Multiselect = false };
        var currentParent = string.IsNullOrWhiteSpace(DestinationRoot) ? null : Directory.GetParent(DestinationRoot)?.FullName;
        if (currentParent is not null && Directory.Exists(currentParent)) dialog.InitialDirectory = currentParent;
        if (dialog.ShowDialog(this) != true) return;
        var folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(_record.RootPath));
        DestinationBox.Text = Path.Combine(dialog.FolderName, folderName);
    }

    private void Migrate_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        if (string.IsNullOrWhiteSpace(DestinationRoot))
        {
            ErrorText.Text = "请输入或选择新路径。";
            return;
        }
        string fullPath;
        try { fullPath = Path.GetFullPath(DestinationRoot); }
        catch (Exception exception)
        {
            ErrorText.Text = $"新路径无效：{exception.Message}";
            return;
        }
        if (Directory.Exists(fullPath) || File.Exists(fullPath))
        {
            ErrorText.Text = "新路径已经存在，请指定一个尚不存在的目录。";
            return;
        }
        var parent = Directory.GetParent(fullPath)?.FullName;
        if (parent is null || !Directory.Exists(parent))
        {
            ErrorText.Text = "新路径的父目录不存在。";
            return;
        }
        DialogResult = true;
    }
}

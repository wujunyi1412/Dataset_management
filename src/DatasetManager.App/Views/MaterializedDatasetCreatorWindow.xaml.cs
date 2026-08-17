using System.IO;
using System.Windows;
using System.Windows.Controls;
using DatasetManager.Core.Models;
using Microsoft.Win32;

namespace DatasetManager.App.Views;

public partial class MaterializedDatasetCreatorWindow : Window
{
    public MaterializedDatasetCreatorWindow(IReadOnlyCollection<DatasetRecord> datasets)
    {
        InitializeComponent();
        ManifestBox.ItemsSource = datasets
            .Where(x => x.Type is DatasetType.Training or DatasetType.Test or DatasetType.Validation)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new ManifestOption(x))
            .ToArray();
        ManifestBox.SelectedIndex = ManifestBox.Items.Count > 0 ? 0 : -1;
    }

    public string DatasetName => NameBox.Text.Trim();
    public string ManifestPath => (ManifestBox.SelectedItem as ManifestOption)?.Record.RootPath ?? string.Empty;
    public string DestinationParent => DestinationBox.Text.Trim();
    public string ImagesFolderName => ImagesFolderBox.Text.Trim();
    public string LabelsFolderName => LabelsFolderBox.Text.Trim();
    public string Notes => NotesBox.Text.Trim();

    private void ManifestBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ManifestBox.SelectedItem is not ManifestOption option)
        {
            ManifestDetailsText.Text = "当前没有可用的训练集、测试集或验证集清单。";
            return;
        }
        ManifestDetailsText.Text = $"清单：{option.Record.RootPath}\n{option.Record.Notes}";
        if (string.IsNullOrWhiteSpace(NameBox.Text))
            NameBox.Text = option.Record.Name;
    }

    private void BrowseDestination_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "选择输出父目录", Multiselect = false };
        if (Directory.Exists(DestinationBox.Text)) dialog.InitialDirectory = DestinationBox.Text;
        if (dialog.ShowDialog(this) == true) DestinationBox.Text = dialog.FolderName;
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        if (string.IsNullOrWhiteSpace(DatasetName)) ErrorText.Text = "请输入数据集名称。";
        else if (!File.Exists(ManifestPath)) ErrorText.Text = "请选择一个存在的数据集清单。";
        else if (!Directory.Exists(DestinationParent)) ErrorText.Text = "请选择一个存在的输出父目录。";
        else if (string.IsNullOrWhiteSpace(ImagesFolderName) || string.IsNullOrWhiteSpace(LabelsFolderName))
            ErrorText.Text = "图片和标签文件夹名称不能为空。";
        else DialogResult = true;
    }
}

public sealed class ManifestOption(DatasetRecord record)
{
    public DatasetRecord Record { get; } = record;
    public string DisplayName => $"[{GetTypeName(Record.Type)}] {Record.Name}　{Record.Composition?.PairCount ?? 0:N0} 对";

    private static string GetTypeName(DatasetType type) => type switch
    {
        DatasetType.Training => "训练集",
        DatasetType.Test => "测试集",
        DatasetType.Validation => "验证集",
        _ => type.ToString()
    };
}

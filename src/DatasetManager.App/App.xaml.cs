using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace DatasetManager.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var logPath = WriteCrashLog(e.Exception);
        MessageBox.Show(
            $"程序遇到未处理错误：\n\n{e.Exception.Message}\n\n错误日志：{logPath}",
            "Dataset Manager 启动失败",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static string WriteCrashLog(Exception exception)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DatasetManager",
                "logs");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(path, exception.ToString());
            return path;
        }
        catch
        {
            return "日志写入失败";
        }
    }
}

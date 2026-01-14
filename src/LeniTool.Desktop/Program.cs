using Avalonia;
using Avalonia.ReactiveUI;
using System;
using System.Reactive;
using System.Threading.Tasks;
using LeniTool.Desktop.Services;
using ReactiveUI;

namespace LeniTool.Desktop;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        CrashLogger.Initialize();

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                CrashLogger.WriteException(ex, "AppDomain.CurrentDomain.UnhandledException");
            else
                CrashLogger.WriteLine($"UnhandledException: {e.ExceptionObject}");
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            CrashLogger.WriteException(e.Exception, "TaskScheduler.UnobservedTaskException");
            e.SetObserved();
        };

        RxApp.DefaultExceptionHandler = Observer.Create<Exception>(ex =>
            CrashLogger.WriteException(ex, "ReactiveUI DefaultExceptionHandler"));

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            CrashLogger.WriteException(ex, "Program.Main");
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseReactiveUI()
            .WithInterFont()
            .LogToTrace();
}

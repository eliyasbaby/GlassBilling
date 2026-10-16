using GlassBilling.Services;

namespace GlassBilling.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp()
    {
        var app = MauiProgram.CreateMauiApp();
        var logger = app.Services.GetRequiredService<AppLogService>();

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            _ = logger.ErrorAsync("Unhandled .NET exception", args.ExceptionObject as Exception
                ?? new Exception(args.ExceptionObject?.ToString() ?? "Unknown exception"));

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            _ = logger.ErrorAsync("Unobserved task exception", args.Exception);
            args.SetObserved();
        };

        UnhandledException += (_, args) =>
            _ = logger.ErrorAsync("Unhandled Windows UI exception", args.Exception);

        return app;
    }
}

using CommunityToolkit.Maui;
using GlassBilling.Services;
using GlassBilling.ViewModels;
using GlassBilling.Views;
using Microsoft.Extensions.Logging;

#if !ANDROID
using QuestPDF.Infrastructure;
#endif

namespace GlassBilling;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
#if !ANDROID
        QuestPDF.Settings.License = LicenseType.Community;
#endif

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Services
        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<BackupService>();
        builder.Services.AddSingleton<PdfService>();

        // ViewModels
        builder.Services.AddTransient<CustomersViewModel>();
        builder.Services.AddSingleton<NewBillViewModel>();   // must be singleton — shared across 3 steps
        builder.Services.AddTransient<BillHistoryViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // Pages
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<CustomersPage>();
        builder.Services.AddSingleton<NewBillStep1Page>();   // singleton so same VM instance is used
        builder.Services.AddSingleton<NewBillStep2Page>();
        builder.Services.AddSingleton<NewBillStep3Page>();
        builder.Services.AddSingleton<ExtraChargesPage>();
        builder.Services.AddTransient<BillPreviewPage>();
        builder.Services.AddTransient<BillHistoryPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        //builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

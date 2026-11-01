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
#if WINDOWS
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush InputBorderBrush = new(
        Microsoft.UI.ColorHelper.FromArgb(255, 148, 163, 184));
    private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush FocusedInputBorderBrush = new(
        Microsoft.UI.ColorHelper.FromArgb(255, 37, 99, 235));
#endif

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

#if WINDOWS
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("GlassBillingInputFocus", (handler, _) =>
            ConfigureWindowsInputFocus(handler.PlatformView));

        Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("GlassBillingInputFocus", (handler, _) =>
            ConfigureWindowsInputFocus(handler.PlatformView));

        Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("GlassBillingInputFocus", (handler, _) =>
            ConfigureWindowsInputFocus(handler.PlatformView));
#endif

        // Services
        builder.Services.AddSingleton<AppLogService>();
        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<BackupService>();
        builder.Services.AddSingleton<PdfService>();

        // ViewModels
        builder.Services.AddTransient<CustomersViewModel>();
        builder.Services.AddSingleton<NewBillViewModel>();   // must be singleton — shared across 3 steps
        builder.Services.AddTransient<BillHistoryViewModel>();
        builder.Services.AddTransient<ReportsViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // Pages
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<CustomersPage>();
        builder.Services.AddSingleton<BillingPage>();   // singleton so same VM instance is used
        builder.Services.AddTransient<BillPreviewPage>();
        builder.Services.AddTransient<BillHistoryPage>();
        builder.Services.AddTransient<ReportsPage>();
        builder.Services.AddTransient<SettingsPage>();

#if DEBUG
        //builder.Logging.AddDebug();
#endif

        return builder.Build();
    }

#if WINDOWS
    private static void ConfigureWindowsInputFocus(Microsoft.UI.Xaml.FrameworkElement control)
    {
        control.GotFocus -= OnWindowsInputGotFocus;
        control.GotFocus += OnWindowsInputGotFocus;
        control.LostFocus -= OnWindowsInputLostFocus;
        control.LostFocus += OnWindowsInputLostFocus;
        SetWindowsInputBorder(control, control.FocusState != Microsoft.UI.Xaml.FocusState.Unfocused);
    }

    private static void OnWindowsInputGotFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.FrameworkElement control)
            SetWindowsInputBorderAfterFocusTransition(control, true);
    }

    private static void OnWindowsInputLostFocus(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.FrameworkElement control)
            SetWindowsInputBorderAfterFocusTransition(control, false);
    }

    private static void SetWindowsInputBorderAfterFocusTransition(
        Microsoft.UI.Xaml.FrameworkElement control,
        bool isFocused)
    {
        if (control is Microsoft.UI.Xaml.Controls.TextBox)
        {
            var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            if (dispatcher is not null && dispatcher.TryEnqueue(() => SetWindowsInputBorder(control, isFocused)))
                return;
        }

        SetWindowsInputBorder(control, isFocused);
    }

    private static void SetWindowsInputBorder(Microsoft.UI.Xaml.FrameworkElement control, bool isFocused)
    {
        var brush = isFocused ? FocusedInputBorderBrush : InputBorderBrush;
        var thickness = new Microsoft.UI.Xaml.Thickness(isFocused ? 2 : 1);

        switch (control)
        {
            case Microsoft.UI.Xaml.Controls.TextBox textBox:
                textBox.BorderBrush = brush;
                textBox.BorderThickness = thickness;
                break;
            case Microsoft.UI.Xaml.Controls.ComboBox comboBox:
                comboBox.BorderBrush = brush;
                comboBox.BorderThickness = thickness;
                break;
        }
    }
#endif
}

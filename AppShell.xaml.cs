using GlassBilling.Views;
using Microsoft.Extensions.DependencyInjection;

namespace GlassBilling;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();

        // Resolve Shell pages through DI because pages use constructor injection.
        HomeContent.Content = services.GetRequiredService<MainPage>();
        CustomersContent.Content = services.GetRequiredService<CustomersPage>();
        BillHistoryContent.Content = services.GetRequiredService<BillHistoryPage>();
        SettingsContent.Content = services.GetRequiredService<SettingsPage>();

        // Register routes for navigation
        Routing.RegisterRoute(nameof(NewBillStep1Page), typeof(NewBillStep1Page));
        Routing.RegisterRoute(nameof(NewBillStep2Page), typeof(NewBillStep2Page));
        Routing.RegisterRoute(nameof(NewBillStep3Page), typeof(NewBillStep3Page));
        Routing.RegisterRoute(nameof(ExtraChargesPage), typeof(ExtraChargesPage));
        Routing.RegisterRoute(nameof(BillPreviewPage), typeof(BillPreviewPage));
        Routing.RegisterRoute(nameof(CustomersPage), typeof(CustomersPage));
        Routing.RegisterRoute(nameof(BillHistoryPage), typeof(BillHistoryPage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
    }
}

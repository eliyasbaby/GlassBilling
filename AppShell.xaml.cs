using GlassBilling.Views;
using Microsoft.Extensions.DependencyInjection;

namespace GlassBilling;

public partial class AppShell : Shell
{
    public AppShell(IServiceProvider services)
    {
        InitializeComponent();
        AppVersionLabel.Text = $"Version {AppInfo.Current.VersionString}";

        // Resolve Shell pages through DI because pages use constructor injection.
        HomeContent.Content = services.GetRequiredService<MainPage>();
        CustomersContent.Content = services.GetRequiredService<CustomersPage>();
        BillHistoryContent.Content = services.GetRequiredService<BillHistoryPage>();
        SettingsContent.Content = services.GetRequiredService<SettingsPage>();

        // Register routes for navigation
        Routing.RegisterRoute(nameof(BillingPage), typeof(BillingPage));
        Routing.RegisterRoute(nameof(BillPreviewPage), typeof(BillPreviewPage));
        Routing.RegisterRoute(nameof(CustomersPage), typeof(CustomersPage));
        Routing.RegisterRoute(nameof(BillHistoryPage), typeof(BillHistoryPage));
        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();
        try
        {
            // Show the current logged-in user in the title bar
            LoggedInUserLabel.Text = Environment.UserName;
        }
        catch
        {
            // ignore if label not found or environment not available
        }
    }
}

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
        ReportsContent.Content = services.GetRequiredService<ReportsPage>();
        SettingsContent.Content = services.GetRequiredService<SettingsPage>();
        BillingContent.Content = services.GetRequiredService<BillingPage>();

        // Register routes for navigation
        Routing.RegisterRoute(nameof(BillPreviewPage), typeof(BillPreviewPage));
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

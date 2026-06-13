using GlassBilling.Services;

namespace GlassBilling.Views;

public partial class MainPage : ContentPage
{
    private readonly DatabaseService _db;

    public MainPage(DatabaseService db)
    {
        InitializeComponent();
        _db = db;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var (bills, customers, revenue) = await _db.GetStatsAsync();
        BillsCount.Text     = bills.ToString();
        CustomersCount.Text = customers.ToString();
        Revenue.Text        = $"₹{revenue:N0}";
    }

    private async void OnNewBillClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync(nameof(NewBillStep1Page));

    private async void OnCustomersClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync(nameof(CustomersPage));

    private async void OnBillHistoryClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync(nameof(BillHistoryPage));

    private async void OnSettingsClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync(nameof(SettingsPage));
}

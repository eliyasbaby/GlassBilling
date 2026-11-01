using GlassBilling.Services;
using GlassBilling.ViewModels;

namespace GlassBilling.Views;

public partial class MainPage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly AppLogService _log;
    private readonly NewBillViewModel _newBillVm;

    public MainPage(DatabaseService db, AppLogService log, NewBillViewModel newBillVm)
    {
        InitializeComponent();
        _db = db;
        _log = log;
        _newBillVm = newBillVm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _log.InfoAsync($"Home opened. Log directory: {_log.LogDirectory}");
        var (bills, customers, revenue) = await _db.GetStatsAsync();
        BillsCount.Text     = bills.ToString();
        CustomersCount.Text = customers.ToString();
        Revenue.Text        = $"₹{revenue:N0}";
    }

    private async void OnNewBillClicked(object sender, EventArgs e)
    {
        await _newBillVm.LoadStep1Async();
        await Shell.Current.GoToAsync($"//{nameof(BillingPage)}");
    }

    private async void OnCustomersClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync($"//{nameof(CustomersPage)}");

    private async void OnBillHistoryClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync($"//{nameof(BillHistoryPage)}");

    private async void OnReportsClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync($"//{nameof(ReportsPage)}");

    private async void OnSettingsClicked(object sender, EventArgs e)
        => await Shell.Current.GoToAsync($"//{nameof(SettingsPage)}");
}

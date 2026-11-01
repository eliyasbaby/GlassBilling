using GlassBilling.ViewModels;

namespace GlassBilling.Views;

public partial class ReportsPage : ContentPage
{
    private readonly ReportsViewModel _vm;

    public ReportsPage(ReportsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadReportsAsync();
    }
}
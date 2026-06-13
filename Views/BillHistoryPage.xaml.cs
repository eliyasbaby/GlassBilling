using GlassBilling.Services;
using GlassBilling.ViewModels;

namespace GlassBilling.Views;

public partial class BillHistoryPage : ContentPage
{
    private readonly BillHistoryViewModel _vm;

    public BillHistoryPage(BillHistoryViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadBillsAsync();
    }
}

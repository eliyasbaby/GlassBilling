using GlassBilling.Services;
using GlassBilling.ViewModels;

namespace GlassBilling.Views;

public partial class CustomersPage : ContentPage
{
    private readonly CustomersViewModel _vm;

    public CustomersPage(CustomersViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadCustomersAsync();
    }
}

using GlassBilling.ViewModels;

namespace GlassBilling.Views;

public partial class NewBillStep1Page : ContentPage
{
    private readonly NewBillViewModel _vm;

    public NewBillStep1Page(NewBillViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadStep1Async();
    }
}

using GlassBilling.ViewModels;

namespace GlassBilling.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _vm;

    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadThicknessTypesAsync();
        await _vm.LoadGlassTypesAsync();
        await _vm.LoadGlassShapesAsync();
    }
}

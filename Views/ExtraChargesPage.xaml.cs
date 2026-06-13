using GlassBilling.ViewModels;

namespace GlassBilling.Views;

public partial class ExtraChargesPage : ContentPage
{
    private readonly NewBillViewModel _vm;

    public ExtraChargesPage(NewBillViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
        _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RefreshTotals();

        // Refresh totals whenever tax percent changes
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.PropertyChanged -= OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NewBillViewModel.TaxPercent)
            or nameof(NewBillViewModel.ExtraCharges))
        {
            RefreshTotals();
        }
    }

    private void RefreshTotals()
    {
        double sub   = _vm.CalcSubTotal();
        double extra = _vm.CalcExtraTotal();
        double tax   = _vm.CalcTax();
        double grand = _vm.CalcGrandTotal();

        SubTotalLbl.Text   = $"₹{sub:N2}";
        ExtraTotalLbl.Text = $"₹{extra:N2}";
        TaxLbl.Text        = $"Tax ({_vm.TaxPercent:N0}%)";
        TaxAmtLbl.Text     = $"₹{tax:N2}";
        GrandTotalLbl.Text = $"₹{grand:N2}";
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnGenerateBillClicked(object sender, EventArgs e)
    {
        LoadingOverlay.IsVisible = true;
        try
        {
            await _vm.GenerateBillCommand.ExecuteAsync(null);
        }
        finally
        {
            LoadingOverlay.IsVisible = false;
        }
    }
}

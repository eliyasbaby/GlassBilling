using GlassBilling.ViewModels;

namespace GlassBilling.Views;

public partial class NewBillStep1Page : ContentPage
{
    private readonly NewBillViewModel _vm;
    private readonly HashSet<BillItemEntry> _hookedEntries = new();

    public NewBillStep1Page(NewBillViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadSinglePageAsync();
        HookEntryChanges();
        RefreshTotals();
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
            or nameof(NewBillViewModel.BillItemEntries)
            or nameof(NewBillViewModel.ExtraCharges))
        {
            HookEntryChanges();
            RefreshTotals();
        }
    }

    private void OnAnyValueChanged(object sender, TextChangedEventArgs e) => ScheduleTotalsRefresh();

    private void OnAnySelectionChanged(object sender, EventArgs e) => ScheduleTotalsRefresh();

    private void OnApplyThicknessClicked(object sender, EventArgs e)
    {
        HookEntryChanges();
        ScheduleTotalsRefresh();
    }

    private void ScheduleTotalsRefresh()
    {
        Dispatcher.Dispatch(async () =>
        {
            await Task.Delay(120);
            RefreshTotals();
        });
    }

    private void HookEntryChanges()
    {
        foreach (var entry in _vm.BillItemEntries)
        {
            if (!_hookedEntries.Add(entry))
                continue;

            entry.PropertyChanged += OnEntryPropertyChanged;
        }
    }

    private void OnEntryPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BillItemEntry.CalculatedTotal)
            or nameof(BillItemEntry.ThicknessType)
            or nameof(BillItemEntry.MeasurementUnit)
            or nameof(BillItemEntry.CuttingAllowance))
        {
            ScheduleTotalsRefresh();
        }
    }

    private void RefreshTotals()
    {
        double sub = _vm.CalcSubTotal();
        double extra = _vm.CalcExtraTotal();
        double tax = _vm.CalcTax();
        double grand = _vm.CalcGrandTotal();

        SubTotalLbl.Text = $"₹{sub:N2}";
        ExtraTotalLbl.Text = $"₹{extra:N2}";
        TaxLbl.Text = $"Tax ({_vm.TaxPercent:N0}%)";
        TaxAmtLbl.Text = $"₹{tax:N2}";
        GrandTotalLbl.Text = $"₹{grand:N2}";
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

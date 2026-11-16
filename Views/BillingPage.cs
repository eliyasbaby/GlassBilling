using GlassBilling.Models;
using GlassBilling.ViewModels;

namespace GlassBilling.Views;

public partial class BillingPage : ContentPage
{
    private readonly NewBillViewModel _vm;
    private readonly HashSet<BillItemEntry> _hookedEntries = new();
    private Customer? _previousCustomerSelection;

    public BillingPage(NewBillViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadSinglePageAsync();
        _previousCustomerSelection = _vm.SelectedCustomer;
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
            or nameof(NewBillViewModel.DiscountAmount)
            or nameof(NewBillViewModel.BillItemEntries)
            or nameof(NewBillViewModel.ExtraCharges))
        {
            HookEntryChanges();
            RefreshTotals();
        }
    }

    private void OnCustomerPickerSelectionChanged(object sender, EventArgs e)
    {
        if (_vm.SelectedCustomer is null)
            return;

        if (_vm.SelectedCustomer.Id == -1)
        {
            var picker = (Picker)sender;
            picker.SelectedItem = _previousCustomerSelection;
            _vm.SelectedCustomer = _previousCustomerSelection;
            _vm.OpenCustomerForm();
            _previousCustomerSelection = _vm.SelectedCustomer;
        }
        else
        {
            _previousCustomerSelection = _vm.SelectedCustomer;
        }
    }

    private void OnAnyValueChanged(object sender, TextChangedEventArgs e) => ScheduleTotalsRefresh();

    private void OnDiscountValueChanged(object sender, TextChangedEventArgs e)
    {
        // An empty Entry cannot be converted to the bound double, so the source
        // otherwise retains the previous discount value.
        if (string.IsNullOrWhiteSpace(e.NewTextValue))
            _vm.DiscountAmount = 0;

        ScheduleTotalsRefresh();
    }

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
        double discount = _vm.CalcDiscount();
        double tax = _vm.CalcTax();
        double grand = _vm.CalcGrandTotal();

        SubTotalLbl.Text = $"₹{sub:N2}";
        ExtraTotalLbl.Text = $"₹{extra:N2}";
        TaxLbl.Text = $"Tax ({_vm.TaxPercent:N0}%)";
        TaxAmtLbl.Text = $"₹{tax:N2}";
        DiscountAmtLbl.Text = $"₹{discount:N2}";
        GrandTotalLbl.Text = $"₹{grand:N0}";
    }

    private async void OnPreviewBillClicked(object sender, EventArgs e)
    {
        LoadingOverlay.IsVisible = true;
        try
        {
            await _vm.PreviewBillCommand.ExecuteAsync(null);
        }
        finally
        {
            LoadingOverlay.IsVisible = false;
        }
    }
}

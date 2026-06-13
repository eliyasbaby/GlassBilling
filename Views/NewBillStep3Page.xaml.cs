using GlassBilling.ViewModels;

namespace GlassBilling.Views;

public partial class NewBillStep3Page : ContentPage
{
    private readonly NewBillViewModel _vm;

    public NewBillStep3Page(NewBillViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Set unit picker to match current entry unit
        var unit = _vm.CurrentEntry?.MeasurementUnit ?? "mm";
        UnitPicker.SelectedIndex = unit switch
        {
            "mm"   => 0,
            "cm"   => 1,
            "inch" => 2,
            _      => 0
        };
        UpdateNextButton();
    }

    private void OnUnitChanged(object sender, EventArgs e)
    {
        if (_vm.CurrentEntry is null) return;
        _vm.CurrentEntry.MeasurementUnit = UnitPicker.SelectedIndex switch
        {
            0 => "mm",
            1 => "cm",
            2 => "inch",
            _ => "mm"
        };
    }

    private void UpdateNextButton()
    {
        bool isLast = _vm.CurrentThicknessIndex >= _vm.BillItemEntries.Count - 1;
        NextBtn.Text = isLast ? "Extra Charges →" : "Next →";
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(_vm.CurrentThicknessIndex))
            UpdateNextButton();
    }
}

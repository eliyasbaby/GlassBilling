using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassBilling.Models;
using GlassBilling.Services;
using GlassBilling.Views;
using System.Collections.ObjectModel;

namespace GlassBilling.ViewModels;

public partial class NewBillViewModel : BaseViewModel
{
    private readonly DatabaseService _db;
    private readonly PdfService _pdf;

    // ── Step 1 ───────────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<Customer> _customers = new();
    [ObservableProperty] private Customer? _selectedCustomer;
    [ObservableProperty] private int _numberOfThicknessTypes = 1;

    // ── Step 2 ───────────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<ThicknessType> _availableThicknessTypes = new();
    [ObservableProperty] private ObservableCollection<ThicknessTypeSelection> _thicknessSelections = new();

    // ── Step 3 ───────────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<BillItemEntry> _billItemEntries = new();
    [ObservableProperty] private int _currentThicknessIndex;
    [ObservableProperty] private BillItemEntry? _currentEntry;

    // ── Step 4 – Extra Charges ────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<ExtraChargeEntry> _extraCharges = new();
    [ObservableProperty] private double _taxPercent = 0;
    [ObservableProperty] private string _notes = string.Empty;

    public NewBillViewModel(DatabaseService db, PdfService pdf)
    {
        _db  = db;
        _pdf = pdf;
        Title = "New Bill";
    }

    // Predefined charge names shown on Step 4
    private static readonly string[] PredefinedChargeNames =
    {
        "Patch Cutting", "Hole", "Hole (Big)", "L Shape", "Wheel Cut",
        "Shape Cut", "Cut Out", "Beveling Charge", "Etching Work",
        "Cutting Charge", "Single Patch"
    };

    private void ResetExtraCharges()
    {
        ExtraCharges.Clear();
        foreach (var name in PredefinedChargeNames)
            ExtraCharges.Add(new ExtraChargeEntry { Name = name, Amount = 0 });
    }

    // ── Step 1 ───────────────────────────────────────────────────────────────

    [RelayCommand]
    public async Task LoadStep1Async()
    {
        // Reset all state for a fresh bill
        ThicknessSelections.Clear();
        BillItemEntries.Clear();
        ResetExtraCharges();
        Notes = string.Empty;

        var list = await _db.GetCustomersAsync();
        Customers.Clear();
        foreach (var c in list) Customers.Add(c);
        SelectedCustomer = Customers.FirstOrDefault();
        NumberOfThicknessTypes = 1;
        TaxPercent = double.TryParse(Preferences.Get("default_tax", "18"), out var t) ? t : 18;
    }

    [RelayCommand]
    private async Task GoToStep2Async()
    {
        if (SelectedCustomer is null)
        {
            await Shell.Current.DisplayAlert("Validation", "Please select a customer.", "OK");
            return;
        }
        if (NumberOfThicknessTypes < 1 || NumberOfThicknessTypes > 20)
        {
            await Shell.Current.DisplayAlert("Validation", "Enter a number between 1 and 20.", "OK");
            return;
        }

        var types = await _db.GetThicknessTypesAsync();
        AvailableThicknessTypes.Clear();
        foreach (var t in types) AvailableThicknessTypes.Add(t);

        ThicknessSelections.Clear();
        for (int i = 0; i < NumberOfThicknessTypes; i++)
        {
            ThicknessSelections.Add(new ThicknessTypeSelection
            {
                Index          = i + 1,
                AvailableTypes = AvailableThicknessTypes,
                SelectedType   = AvailableThicknessTypes.FirstOrDefault()
            });
        }

        await Shell.Current.GoToAsync(nameof(NewBillStep2Page));
    }

    // ── Step 2 ───────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task GoToStep3Async()
    {
        if (ThicknessSelections.Any(s => s.SelectedType is null))
        {
            await Shell.Current.DisplayAlert("Validation", "Please select all thickness types.", "OK");
            return;
        }

        BillItemEntries.Clear();
        foreach (var sel in ThicknessSelections)
        {
            var entry = new BillItemEntry
            {
                ThicknessType    = sel.SelectedType!,
                MeasurementUnit  = "mm",
                CuttingAllowance = 0,
                Shape            = "Block"
            };
            entry.AddRow();
            BillItemEntries.Add(entry);
        }

        CurrentThicknessIndex = 0;
        CurrentEntry = BillItemEntries[0];

        await Shell.Current.GoToAsync(nameof(NewBillStep3Page));
    }

    // ── Step 3 ───────────────────────────────────────────────────────────────

    [RelayCommand] private void AddMeasurementRow()    => CurrentEntry?.AddRow();
    [RelayCommand] private void RemoveLastRow()        => CurrentEntry?.RemoveLastRow();
    [RelayCommand] private void RemoveRow(MeasurementEntryRow row) => CurrentEntry?.RemoveRow(row);

    [RelayCommand]
    private async Task NextThicknessOrFinishAsync()
    {
        if (CurrentEntry is null) return;

        if (!CurrentEntry.HasValidRows)
        {
            await Shell.Current.DisplayAlert("Validation",
                "Enter at least one measurement with Length and Width > 0.", "OK");
            return;
        }

        if (CurrentThicknessIndex < BillItemEntries.Count - 1)
        {
            CurrentThicknessIndex++;
            CurrentEntry = BillItemEntries[CurrentThicknessIndex];
        }
        else
        {
            // All thicknesses done → go to Step 4 (Extra Charges)
            await Shell.Current.GoToAsync(nameof(ExtraChargesPage));
        }
    }

    [RelayCommand]
    private void PreviousThickness()
    {
        if (CurrentThicknessIndex > 0)
        {
            CurrentThicknessIndex--;
            CurrentEntry = BillItemEntries[CurrentThicknessIndex];
        }
    }

    // ── Step 4 – Extra Charges ────────────────────────────────────────────────

    [RelayCommand]
    private void AddExtraCharge()
    {
        ExtraCharges.Add(new ExtraChargeEntry { Name = "Transportation", Amount = 0 });
    }

    [RelayCommand]
    private void RemoveExtraCharge(ExtraChargeEntry ec) => ExtraCharges.Remove(ec);

    public double CalcSubTotal()    => BillItemEntries.Sum(e => e.CalcTotal());
    public double CalcExtraTotal()  => ExtraCharges.Sum(e => e.Amount);
    public double CalcTax()         => (CalcSubTotal() + CalcExtraTotal()) * TaxPercent / 100.0;
    public double CalcGrandTotal()  => CalcSubTotal() + CalcExtraTotal() + CalcTax();

    [RelayCommand]
    private async Task GenerateBillAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var bill = new Bill
            {
                CustomerId = SelectedCustomer!.Id,
                Customer   = SelectedCustomer,
                BillDate   = DateTime.Now,
                TaxPercent = TaxPercent,
                Notes      = Notes
            };

            foreach (var entry in BillItemEntries)
            {
                var billItem = new BillItem
                {
                    ThicknessTypeId  = entry.ThicknessType.Id,
                    ThicknessName    = entry.ThicknessType.Name,
                    GlassDescription = entry.GlassDescription,
                    MeasurementUnit  = entry.MeasurementUnit,
                    CuttingAllowance = entry.CuttingAllowance,
                    PricePerSqFt     = entry.ThicknessType.PricePerSqFt,
                    Shape            = entry.Shape,
                };

                int rowNum = 1;
                foreach (var r in entry.Rows.Where(r => r.Length > 0 && r.Width > 0))
                {
                    double cl   = r.Length + entry.CuttingAllowance;
                    double cw   = r.Width  + entry.CuttingAllowance;
                    double area = MeasurementRow.CalcAreaSqFt(r.Length, r.Width, entry.MeasurementUnit, r.Quantity);
                    double cArea= MeasurementRow.CalcAreaSqFt(cl, cw, entry.MeasurementUnit, r.Quantity);

                    billItem.Measurements.Add(new MeasurementRow
                    {
                        Length        = r.Length,
                        Width         = r.Width,
                        ChargeLength  = cl,
                        ChargeWidth   = cw,
                        Unit          = entry.MeasurementUnit,
                        Quantity      = r.Quantity,
                        AreaInSqFt    = area,
                        ChargeAreaSqFt= cArea,
                        RowNumber     = rowNum++
                    });
                    billItem.TotalChargeAreaSqFt += cArea;
                }

                billItem.TotalAmount = billItem.TotalChargeAreaSqFt * billItem.PricePerSqFt;
                bill.Items.Add(billItem);
            }

            // Only include charges with a non-zero amount
            bill.ExtraCharges = ExtraCharges
                .Where(e => e.Amount > 0)
                .Select(e => new ExtraCharge { Name = e.Name, Amount = e.Amount })
                .ToList();

            bill.Recalculate();

            int billId = await _db.SaveBillAsync(bill);
            var saved  = await _db.GetBillWithDetailsAsync(billId);

            var navParams = new Dictionary<string, object> { { "Bill", saved! } };
            await Shell.Current.GoToAsync(nameof(BillPreviewPage), navParams);
        }
        finally { IsBusy = false; }
    }
}

// ── Supporting VM models ──────────────────────────────────────────────────────

public partial class ThicknessTypeSelection : ObservableObject
{
    public int Index { get; set; }
    public ObservableCollection<ThicknessType> AvailableTypes { get; set; } = new();
    [ObservableProperty] private ThicknessType? _selectedType;
}

public partial class BillItemEntry : ObservableObject
{
    public ThicknessType ThicknessType  { get; set; } = new();
    [ObservableProperty] private string _measurementUnit  = "mm";
    [ObservableProperty] private double _cuttingAllowance = 0;
    [ObservableProperty] private string _shape            = "Block";
    [ObservableProperty] private string _glassDescription = "Toughened Plain Glass";

    public ObservableCollection<MeasurementEntryRow> Rows { get; } = new();

    public bool HasValidRows => Rows.Any(r => r.Length > 0 && r.Width > 0);

    public double CalcTotal()
    {
        double area = Rows
            .Where(r => r.Length > 0 && r.Width > 0)
            .Sum(r => {
                double cl = r.Length + CuttingAllowance;
                double cw = r.Width  + CuttingAllowance;
                return MeasurementRow.CalcAreaSqFt(cl, cw, MeasurementUnit, r.Quantity);
            });
        return area * ThicknessType.PricePerSqFt;
    }

    public void AddRow()        => Rows.Add(new MeasurementEntryRow { RowNumber = Rows.Count + 1 });
    public void RemoveLastRow() { if (Rows.Count > 1) Rows.RemoveAt(Rows.Count - 1); }
    public void RemoveRow(MeasurementEntryRow r) { if (Rows.Count > 1) Rows.Remove(r); }
}

public partial class MeasurementEntryRow : ObservableObject
{
    public int    RowNumber { get; set; }
    [ObservableProperty] private double _length;
    [ObservableProperty] private double _width;
    [ObservableProperty] private int    _quantity = 1;
}

public partial class ExtraChargeEntry : ObservableObject
{
    [ObservableProperty] private string _name   = string.Empty;
    [ObservableProperty] private double _amount = 0;
}

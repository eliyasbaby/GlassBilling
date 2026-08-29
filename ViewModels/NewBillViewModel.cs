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
    private bool _isInitialized;

    // ── Step 1 ───────────────────────────────────────────────────────────────
    [ObservableProperty] private ObservableCollection<Customer> _customers = new();
    [ObservableProperty] private Customer? _selectedCustomer;
    [ObservableProperty] private int _numberOfThicknessTypes = 1;
    [ObservableProperty] private bool _isCustomerFormVisible;
    [ObservableProperty] private string _newCustomerName = string.Empty;
    [ObservableProperty] private string _newCustomerLocation = string.Empty;
    [ObservableProperty] private string _newCustomerPhone = string.Empty;
    [ObservableProperty] private string _newCustomerAddress = string.Empty;

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
    [ObservableProperty] private double _discountAmount = 0;
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
        for (int i = 0; i < PredefinedChargeNames.Length; i++)
        {
            ExtraCharges.Add(new ExtraChargeEntry
            {
                Name = PredefinedChargeNames[i],
                Amount = 0,
                ColumnIndex = i % 4,
                RowIndex = i / 4,
                TabIndex = i * 2
            });
        }

        RefreshExtraChargeLayoutIndexes();
    }

    private void RefreshExtraChargeLayoutIndexes()
    {
        for (int i = 0; i < ExtraCharges.Count; i++)
        {
            ExtraCharges[i].ColumnIndex = i % 4;
            ExtraCharges[i].RowIndex = i / 4;
            ExtraCharges[i].TabIndex = i * 2;
        }
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
        RefreshCustomers(list);
        SelectedCustomer = Customers.Skip(1).FirstOrDefault();
        IsCustomerFormVisible = false;
        NumberOfThicknessTypes = 1;
        try
        {
            var s = await _db.GetSettingsAsync();
            TaxPercent = s.DefaultTax;
        }
        catch
        {
            TaxPercent = double.TryParse(Preferences.Get("default_tax", "18"), out var t) ? t : 18;
        }
    }

    [RelayCommand]
    public async Task LoadSinglePageAsync()
    {
        var customers = await _db.GetCustomersAsync();
        RefreshCustomers(customers);

        var types = await _db.GetThicknessTypesAsync();
        AvailableThicknessTypes.Clear();
        foreach (var t in types) AvailableThicknessTypes.Add(t);

        if (!_isInitialized)
        {
            ThicknessSelections.Clear();
            BillItemEntries.Clear();
            ResetExtraCharges();
            Notes = string.Empty;
            SelectedCustomer = Customers.Skip(1).FirstOrDefault();
            IsCustomerFormVisible = false;
            NumberOfThicknessTypes = 1;
            BuildBillItemEntries();

            try
            {
                var s = await _db.GetSettingsAsync();
                TaxPercent = s.DefaultTax;
            }
            catch
            {
                TaxPercent = double.TryParse(Preferences.Get("default_tax", "18"), out var t) ? t : 18;
            }

            _isInitialized = true;
            return;
        }

        if (SelectedCustomer is null)
            SelectedCustomer = Customers.FirstOrDefault();

        if (BillItemEntries.Count == 0)
            BuildBillItemEntries();

        if (ThicknessSelections.Count == 0 && NumberOfThicknessTypes > 0)
        {
            ThicknessSelections.Clear();
            for (int i = 0; i < NumberOfThicknessTypes; i++)
            {
                ThicknessSelections.Add(new ThicknessTypeSelection
                {
                    Index = i + 1,
                    AvailableTypes = AvailableThicknessTypes,
                    SelectedType = AvailableThicknessTypes.FirstOrDefault()
                });
            }
        }
    }

    private static Customer CreateAddCustomerOption() => new()
    {
        Id = -1,
        Name = "➕ Add New Customer..."
    };

    private void RefreshCustomers(IEnumerable<Customer> customers)
    {
        Customers.Clear();
        Customers.Add(CreateAddCustomerOption());
        foreach (var customer in customers)
            Customers.Add(customer);

        if (SelectedCustomer is null || SelectedCustomer.Id == -1 || !Customers.Contains(SelectedCustomer))
            SelectedCustomer = Customers.Skip(1).FirstOrDefault();
    }

    [RelayCommand]
    public void OpenCustomerForm()
    {
        NewCustomerName = string.Empty;
        NewCustomerLocation = string.Empty;
        NewCustomerPhone = string.Empty;
        NewCustomerAddress = string.Empty;
        IsCustomerFormVisible = true;
    }

    [RelayCommand]
    public void CancelCustomerForm() => IsCustomerFormVisible = false;

    [RelayCommand]
    public async Task SaveCustomerFromFormAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCustomerName))
        {
            await Shell.Current.DisplayAlert("Validation", "Customer name is required.", "OK");
            return;
        }

        var customer = new Customer
        {
            Name = NewCustomerName.Trim(),
            Location = NewCustomerLocation.Trim(),
            Phone = NewCustomerPhone.Trim(),
            Address = NewCustomerAddress.Trim()
        };

        await _db.SaveCustomerAsync(customer);
        var customers = await _db.GetCustomersAsync();
        RefreshCustomers(customers);
        SelectedCustomer = Customers.FirstOrDefault(c => c.Id == customer.Id);
        IsCustomerFormVisible = false;
    }

    [RelayCommand]
    public void RebuildBillItemEntries()
    {
        NumberOfThicknessTypes = Math.Clamp(NumberOfThicknessTypes, 1, 20);
        BuildBillItemEntries();
    }

    private void BuildBillItemEntries()
    {
        BillItemEntries.Clear();

        for (int i = 0; i < NumberOfThicknessTypes; i++)
        {
            var entry = new BillItemEntry
            {
                ThicknessType = AvailableThicknessTypes.FirstOrDefault() ?? new ThicknessType(),
                MeasurementUnit = "mm",
                CuttingAllowance = 0,
                Shape = "Block"
            };

            foreach (var type in AvailableThicknessTypes)
                entry.AvailableTypes.Add(type);

            entry.AddRow();
            BillItemEntries.Add(entry);
        }

        OnPropertyChanged(nameof(BillItemEntries));
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
    [RelayCommand] private void AddRowToEntry(BillItemEntry entry) => entry.AddRow();
    [RelayCommand] private void RemoveLastRowFromEntry(BillItemEntry entry) => entry.RemoveLastRow();

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
        ExtraCharges.Add(new ExtraChargeEntry
        {
            Name = "Transportation",
            Amount = 0,
            ColumnIndex = ExtraCharges.Count % 4,
            RowIndex = ExtraCharges.Count / 4,
            TabIndex = ExtraCharges.Count * 2
        });

        RefreshExtraChargeLayoutIndexes();
    }

    [RelayCommand]
    private void RemoveExtraCharge(ExtraChargeEntry ec)
    {
        ExtraCharges.Remove(ec);
        RefreshExtraChargeLayoutIndexes();
    }

    public double CalcSubTotal()    => BillItemEntries.Sum(e => e.CalcTotal());
    public double CalcExtraTotal()  => ExtraCharges.Sum(e => e.Amount);
    public double CalcDiscount()    => DiscountAmount;
    public double CalcTax()         => (CalcSubTotal() + CalcExtraTotal() - CalcDiscount()) * TaxPercent / 100.0;
    public double CalcGrandTotal()  => CalcSubTotal() + CalcExtraTotal() + CalcTax() - CalcDiscount();

    [RelayCommand]
    private async Task GenerateBillAsync()
    {
        if (IsBusy) return;

        if (SelectedCustomer is null)
        {
            await Shell.Current.DisplayAlert("Validation", "Please select a customer.", "OK");
            return;
        }

        if (BillItemEntries.Count == 0)
        {
            await Shell.Current.DisplayAlert("Validation", "Please add at least one thickness section.", "OK");
            return;
        }

        if (BillItemEntries.Any(e => !e.HasValidRows))
        {
            await Shell.Current.DisplayAlert("Validation",
                "Each thickness section needs at least one measurement with Length and Width greater than 0.", "OK");
            return;
        }

        IsBusy = true;
        try
        {
            var bill = new Bill
            {
                CustomerId      = SelectedCustomer!.Id,
                Customer        = SelectedCustomer,
                BillDate        = DateTime.Now,
                TaxPercent      = TaxPercent,
                DiscountAmount  = DiscountAmount,
                Notes           = Notes
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
                    // Add cutting allowance and 2" wastage (converted to the current unit)
                    double extra = entry.MeasurementUnit switch
                    {
                        "mm"   => 2.0 * 25.4,
                        "cm"   => 2.0 * 2.54,
                        "inch" => 2.0,
                        "feet" => 2.0 / 12.0,
                        _       => 2.0 * 25.4
                    };

                    double cl   = r.Length + entry.CuttingAllowance + extra;
                    double cw   = r.Width  + entry.CuttingAllowance + extra;
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
    [ObservableProperty] private ThicknessType _thicknessType = new();
    [ObservableProperty] private string _measurementUnit  = "mm";
    [ObservableProperty] private double _cuttingAllowance = 0;
    [ObservableProperty] private string _shape            = "Block";
    [ObservableProperty] private string _glassDescription = "Clear glass";

    public ObservableCollection<ThicknessType> AvailableTypes { get; } = new();
    public ObservableCollection<MeasurementEntryRow> Rows { get; } = new();

    public bool HasValidRows => Rows.Any(r => r.Length > 0 && r.Width > 0);
    public double CalculatedTotal => CalcTotal();

    public double CalcTotal()
    {
        double area = Rows
            .Where(r => r.Length > 0 && r.Width > 0)
            .Sum(r => {
                double extra = MeasurementUnit switch
                {
                    "mm"   => 2.0 * 25.4,
                    "cm"   => 2.0 * 2.54,
                    "inch" => 2.0,
                    "feet" => 2.0 / 12.0,
                    _       => 2.0 * 25.4
                };

                double cl = r.Length + CuttingAllowance + extra;
                double cw = r.Width  + CuttingAllowance + extra;
                return MeasurementRow.CalcAreaSqFt(cl, cw, MeasurementUnit, r.Quantity);
            });
        return area * ThicknessType.PricePerSqFt;
    }

    partial void OnThicknessTypeChanged(ThicknessType value) => NotifyTotalChanged();
    partial void OnMeasurementUnitChanged(string value) => NotifyTotalChanged();
    partial void OnCuttingAllowanceChanged(double value) => NotifyTotalChanged();

    public void AddRow()
    {
        var row = new MeasurementEntryRow { RowNumber = Rows.Count + 1 };
        row.PropertyChanged += (_, _) => NotifyTotalChanged();
        Rows.Add(row);
        NotifyTotalChanged();
    }

    public void RemoveLastRow()
    {
        if (Rows.Count <= 1) return;
        Rows.RemoveAt(Rows.Count - 1);
        RenumberRows();
        NotifyTotalChanged();
    }

    public void RemoveRow(MeasurementEntryRow r)
    {
        if (Rows.Count <= 1) return;
        Rows.Remove(r);
        RenumberRows();
        NotifyTotalChanged();
    }

    private void RenumberRows()
    {
        for (int i = 0; i < Rows.Count; i++)
            Rows[i].RowNumber = i + 1;
    }

    private void NotifyTotalChanged()
    {
        OnPropertyChanged(nameof(CalculatedTotal));
        OnPropertyChanged(nameof(HasValidRows));
    }
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
    [ObservableProperty] private int _columnIndex;
    [ObservableProperty] private int _rowIndex;
    [ObservableProperty] private int _tabIndex;
}

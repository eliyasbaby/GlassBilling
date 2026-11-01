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
    private List<GlassType> _glassTypes = new();
    private bool _isInitialized;
    private Bill? _editingBill;

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
    [ObservableProperty] private bool _isEditingBill;
    [ObservableProperty] private int _editingBillId;


    public NewBillViewModel(DatabaseService db, PdfService pdf)
    {
        _db  = db;
        _pdf = pdf;
        Title = "New Bill";
    }

    private void UpdateTitle()
    {
        Title = IsEditingBill ? "Edit Bill" : "New Bill";
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

    private void LoadExtraChargesForEditing(IEnumerable<ExtraCharge> savedCharges)
    {
        var entries = new List<ExtraChargeEntry>();
        var byName = new Dictionary<string, ExtraChargeEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in PredefinedChargeNames)
        {
            var entry = new ExtraChargeEntry
            {
                Name = name,
                Amount = 0
            };
            entries.Add(entry);
            byName[name] = entry;
        }

        foreach (var charge in savedCharges.Where(c => !string.IsNullOrWhiteSpace(c.Name)))
        {
            if (byName.TryGetValue(charge.Name, out var existing))
            {
                existing.Amount = charge.Amount;
                continue;
            }

            var customEntry = new ExtraChargeEntry
            {
                Name = charge.Name,
                Amount = charge.Amount
            };
            entries.Add(customEntry);
            byName[charge.Name] = customEntry;
        }

        ExtraCharges.Clear();
        foreach (var entry in entries)
            ExtraCharges.Add(entry);

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

    public async Task LoadBillForEditingAsync(Bill bill)
    {
        _editingBill = bill;
        IsEditingBill = true;
        EditingBillId = bill.Id;
        UpdateTitle();

        var allTypes = await _db.GetThicknessTypesAsync(false);
        AvailableThicknessTypes.Clear();
        foreach (var type in allTypes) AvailableThicknessTypes.Add(type);
        _glassTypes = await _db.GetGlassTypesAsync();

        var customers = await _db.GetCustomersAsync();
        RefreshCustomers(customers);
        SelectedCustomer = customers.FirstOrDefault(c => c.Id == bill.CustomerId) ?? Customers.Skip(1).FirstOrDefault();

        Notes = bill.Notes;
        TaxPercent = bill.TaxPercent;
        DiscountAmount = bill.DiscountAmount;

        RestoreBillEntries(bill);
        LoadExtraChargesForEditing(bill.ExtraCharges);
        if (ExtraCharges.Count == 0)
            ResetExtraCharges();

        IsCustomerFormVisible = false;
        OnPropertyChanged(nameof(BillItemEntries));
        OnPropertyChanged(nameof(NumberOfThicknessTypes));
    }

    private void RestoreBillEntries(Bill bill)
    {
        NumberOfThicknessTypes = Math.Max(1, bill.Items.Count);
        BillItemEntries.Clear();

        foreach (var item in bill.Items)
        {
            var matchingType = AvailableThicknessTypes.FirstOrDefault(t => t.Id == item.ThicknessTypeId)
                ?? AvailableThicknessTypes.FirstOrDefault()
                ?? new ThicknessType
                {
                    Id = item.ThicknessTypeId,
                    Name = item.ThicknessName,
                    PricePerSqFt = item.PricePerSqFt
                };

            var persistedRate = item.PricePerSqFt;
            if (Math.Abs(persistedRate) < 0.0001 && Math.Abs(matchingType.PricePerSqFt) > 0.0001)
                persistedRate = matchingType.PricePerSqFt;

            var entry = new BillItemEntry
            {
                MeasurementUnit = item.MeasurementUnit,
                CuttingAllowance = item.CuttingAllowance,
                Shape = item.Shape,
                GlassDescription = item.GlassDescription
            };

            foreach (var type in AvailableThicknessTypes)
                entry.AvailableTypes.Add(type);

            entry.ThicknessType = entry.AvailableTypes.FirstOrDefault(t => t.Id == matchingType.Id) ?? matchingType;
            entry.Rate = persistedRate;
            entry.EnsureGlassTypes(_glassTypes, item.GlassDescription);

            foreach (var row in item.Measurements)
            {
                entry.Rows.Add(new MeasurementEntryRow
                {
                    RowNumber = row.RowNumber,
                    Length = row.Length,
                    Width = row.Width,
                    Quantity = row.Quantity
                });
            }

            if (entry.Rows.Count == 0)
                entry.AddRow();

            BillItemEntries.Add(entry);
        }
    }

    [RelayCommand]
    public async Task LoadStep1Async()
    {
        _editingBill = null;
        IsEditingBill = false;
        EditingBillId = 0;
        UpdateTitle();
        CurrentEntry = null;
        CurrentThicknessIndex = 0;

        // Reset all state for a fresh bill
        ThicknessSelections.Clear();
        BillItemEntries.Clear();
        ResetExtraCharges();
        Notes = string.Empty;
        DiscountAmount = 0;
        NewCustomerName = string.Empty;
        NewCustomerLocation = string.Empty;
        NewCustomerPhone = string.Empty;
        NewCustomerAddress = string.Empty;

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
        if (IsEditingBill)
        {
            var customers = await _db.GetCustomersAsync();
            RefreshCustomers(customers);

            var types = await _db.GetThicknessTypesAsync(false);
            AvailableThicknessTypes.Clear();
            foreach (var t in types) AvailableThicknessTypes.Add(t);
            _glassTypes = await _db.GetGlassTypesAsync();

            if (_editingBill is not null)
                RestoreBillEntries(_editingBill);

            if (SelectedCustomer is null)
                SelectedCustomer = Customers.FirstOrDefault();

            return;
        }

        var customersList = await _db.GetCustomersAsync();
        RefreshCustomers(customersList);

        var typesList = await _db.GetThicknessTypesAsync();
        AvailableThicknessTypes.Clear();
        foreach (var t in typesList) AvailableThicknessTypes.Add(t);
        _glassTypes = await _db.GetGlassTypesAsync();

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
                Shape = "Block",
                Rate = AvailableThicknessTypes.FirstOrDefault()?.PricePerSqFt ?? 0
            };

            entry.EnsureGlassTypes(_glassTypes);

            foreach (var type in AvailableThicknessTypes)
                entry.AvailableTypes.Add(type);

            entry.AddRow();
            BillItemEntries.Add(entry);
        }

        OnPropertyChanged(nameof(BillItemEntries));
    }

    [RelayCommand] private void AddMeasurementRow()    => CurrentEntry?.AddRow();
    [RelayCommand] private void RemoveLastRow()        => CurrentEntry?.RemoveLastRow();
    [RelayCommand] private void RemoveRow(MeasurementEntryRow row) => CurrentEntry?.RemoveRow(row);
    [RelayCommand] private void AddRowToEntry(BillItemEntry entry) => entry.AddRow();
    [RelayCommand] private void RemoveLastRowFromEntry(BillItemEntry entry) => entry.RemoveLastRow();

    // ── Extra Charges ────────────────────────────────────────────────

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

        if (EditingBillId == 0)
        {
            if (!WorkstationCodeSettings.IsValid(WorkstationCodeSettings.GetCode()))
            {
                await Shell.Current.DisplayAlert(
                    "Workstation Code Required",
                    "Set a valid, unique workstation code in Settings before creating a new bill.",
                    "OK");
                return;
            }
        }

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
            var existing = EditingBillId > 0 ? await _db.GetBillWithDetailsAsync(EditingBillId) : null;
            var bill = new Bill
            {
                Id              = EditingBillId,
                CustomerId      = SelectedCustomer!.Id,
                Customer        = SelectedCustomer,
                BillDate        = DateTime.Now,
                TaxPercent      = TaxPercent,
                DiscountAmount  = DiscountAmount,
                IsConfirmed     = existing?.IsConfirmed ?? false,
                Notes           = Notes,
                BillNumber      = existing?.BillNumber ?? string.Empty
            };

            // Populate salesman name from settings or fallback to environment user
            try
            {
                var s = await _db.GetSettingsAsync();
                bill.SalesmanName = !string.IsNullOrWhiteSpace(s?.SalesmanName)
                    ? s.SalesmanName
                    : Environment.UserName;
            }
            catch
            {
                bill.SalesmanName = Environment.UserName;
            }

            foreach (var entry in BillItemEntries)
            {
                var billItem = new BillItem
                {
                    ThicknessTypeId  = entry.ThicknessType.Id,
                    ThicknessName    = entry.ThicknessType.Name,
                    GlassDescription = entry.GlassDescription,
                    MeasurementUnit  = entry.MeasurementUnit,
                    CuttingAllowance = entry.CuttingAllowance,
                    PricePerSqFt     = entry.Rate,
                    Shape            = entry.Shape,
                };

                int rowNum = 1;
                foreach (var r in entry.Rows.Where(r => r.Length > 0 && r.Width > 0))
                {
                    double extra = entry.WastageInMeasurementUnit;

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

            IsEditingBill = false;
            EditingBillId = 0;
            UpdateTitle();

            var navParams = new Dictionary<string, object> { { "BillId", billId } };
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
    private readonly Dictionary<string, double> _wastageByGlassType = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty] private ThicknessType _thicknessType = new();
    [ObservableProperty] private string _measurementUnit  = "mm";
    [ObservableProperty] private double _cuttingAllowance = 0;
    private double _rate;
    [ObservableProperty] private string _shape            = "Block";
    [ObservableProperty] private string _glassDescription = "Clear glass";

    public double Rate
    {
        get => _rate;
        set
        {
            if (SetProperty(ref _rate, value))
                NotifyTotalChanged();
        }
    }

    public ObservableCollection<ThicknessType> AvailableTypes { get; } = new();
    public ObservableCollection<string> AvailableGlassTypes { get; } = new();
    public ObservableCollection<MeasurementEntryRow> Rows { get; } = new();

    public void EnsureGlassTypes(IEnumerable<GlassType> glassTypes, string? currentValue = null)
    {
        AvailableGlassTypes.Clear();
        _wastageByGlassType.Clear();
        foreach (var glassType in glassTypes)
        {
            if (string.IsNullOrWhiteSpace(glassType.Name))
                continue;

            if (!AvailableGlassTypes.Contains(glassType.Name))
                AvailableGlassTypes.Add(glassType.Name);
            _wastageByGlassType[glassType.Name] = glassType.WastageInches;
        }

        if (!string.IsNullOrWhiteSpace(currentValue) && !AvailableGlassTypes.Contains(currentValue))
        {
            AvailableGlassTypes.Add(currentValue);
            _wastageByGlassType[currentValue] = 2;
        }

        if (!string.IsNullOrWhiteSpace(currentValue))
            GlassDescription = currentValue;
        else if (!AvailableGlassTypes.Contains(GlassDescription))
            GlassDescription = AvailableGlassTypes.FirstOrDefault() ?? string.Empty;
    }

    private double SelectedWastageInches =>
        _wastageByGlassType.TryGetValue(GlassDescription, out var wastageInches) ? wastageInches : 2;

    public double WastageInMeasurementUnit => MeasurementUnit switch
    {
        "mm" => SelectedWastageInches * 25.4,
        "cm" => SelectedWastageInches * 2.54,
        "inch" => SelectedWastageInches,
        "feet" => SelectedWastageInches / 12.0,
        _ => SelectedWastageInches * 25.4
    };

    public bool HasValidRows => Rows.Any(r => r.Length > 0 && r.Width > 0);
    public double CalculatedTotal => CalcTotal();

    public double CalcTotal()
    {
        double area = Rows
            .Where(r => r.Length > 0 && r.Width > 0)
            .Sum(r => {
                double extra = WastageInMeasurementUnit;

                double cl = r.Length + CuttingAllowance + extra;
                double cw = r.Width  + CuttingAllowance + extra;
                return MeasurementRow.CalcAreaSqFt(cl, cw, MeasurementUnit, r.Quantity);
            });
        return area * Rate;
    }

    partial void OnThicknessTypeChanged(ThicknessType value)
    {
        if (value is null)
            return;

        Rate = value.PricePerSqFt;
        NotifyTotalChanged();
    }

    partial void OnMeasurementUnitChanged(string value) => NotifyTotalChanged();
    partial void OnCuttingAllowanceChanged(double value) => NotifyTotalChanged();
    partial void OnGlassDescriptionChanged(string value) => NotifyTotalChanged();

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

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassBilling.Models;
using GlassBilling.Services;
using System.Collections.ObjectModel;

namespace GlassBilling.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    private readonly DatabaseService _db;
    private readonly BackupService _backup;

    [ObservableProperty] private ObservableCollection<ThicknessType> _thicknessTypes = new();
    [ObservableProperty] private ObservableCollection<GlassType> _glassTypes = new();
    [ObservableProperty] private ObservableCollection<GlassShape> _glassShapes = new();
    [ObservableProperty] private string _companyName    = string.Empty;
    [ObservableProperty] private string _companyAddress = string.Empty;
    [ObservableProperty] private string _companyPhone   = string.Empty;
    [ObservableProperty] private string _companyEmail   = string.Empty;
    [ObservableProperty] private double _defaultTax     = 0;
    [ObservableProperty] private string _companyGstin = string.Empty;
    [ObservableProperty] private string _workstationCode = string.Empty;

    // Bank details
    [ObservableProperty] private string _bankName    = string.Empty;
    [ObservableProperty] private string _bankAccount = string.Empty;
    [ObservableProperty] private string _bankIfsc    = string.Empty;
    [ObservableProperty] private string _bankBranch  = string.Empty;
    [ObservableProperty] private string _upiId       = string.Empty;

    // Terms & conditions
    [ObservableProperty] private string _termsConditions = string.Empty;

    // Edit form
    [ObservableProperty] private bool _isEditingThickness;
    [ObservableProperty] private ThicknessType _editingThickness = new();
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private double _editPrice;

    [ObservableProperty] private bool _isEditingGlassType;
    [ObservableProperty] private GlassType _editingGlassType = new();
    [ObservableProperty] private string _editGlassTypeName = string.Empty;
    [ObservableProperty] private double _editGlassWastageInches = 2;

    [ObservableProperty] private bool _isEditingGlassShape;
    [ObservableProperty] private GlassShape _editingGlassShape = new();
    [ObservableProperty] private string _editGlassShapeName = string.Empty;

    public SettingsViewModel(DatabaseService db, BackupService backup)
    {
        _db     = db;
        _backup = backup;
        Title   = "Settings";
        Task.Run(async () => await LoadCompanyInfoAsync());
    }
    private async Task LoadCompanyInfoAsync()
    {
        try
        {
            var s = await _db.GetSettingsAsync();
            WorkstationCode = WorkstationCodeSettings.GetCode();
            CompanyName = s.CompanyName; CompanyAddress = s.CompanyAddress;
            CompanyPhone = s.CompanyPhone; CompanyEmail = s.CompanyEmail; 
            CompanyGstin = s.CompanyGstin;DefaultTax = s.DefaultTax;

            BankName = s.BankName; BankAccount = s.BankAccount; BankIfsc = s.BankIfsc;
            BankBranch = s.BankBranch; UpiId = s.UpiId;

            TermsConditions = string.IsNullOrWhiteSpace(s.TermsConditions)
                ? Preferences.Get("terms_conditions", "1. Goods once sold will not be taken back or exchanged.\n2. All disputes are subject to local jurisdiction only.\n3. Payment due within 30 days of invoice date.\n4. Breakage during transport is not our responsibility.\n5. Please check material carefully at the time of delivery.")
                : s.TermsConditions;
        }
        catch
        {
            // fallback to preferences
            CompanyName    = Preferences.Get("company_name",    "Glass Works Pvt. Ltd.");
            CompanyAddress = Preferences.Get("company_address", "123, Glass Street, Industrial Area, City – 600001");
            CompanyPhone   = Preferences.Get("company_phone",   "+91 98765 43210");
            CompanyEmail   = Preferences.Get("company_email",   "info@glassworks.com");
            CompanyGstin   = Preferences.Get("company_gstin",    string.Empty);
            DefaultTax     = double.TryParse(Preferences.Get("default_tax", "18"), out var t) ? t : 18;

            BankName    = Preferences.Get("bank_name",    "State Bank of India");
            BankAccount = Preferences.Get("bank_account", "1234567890");
            BankIfsc    = Preferences.Get("bank_ifsc",    "SBIN0001234");
            BankBranch  = Preferences.Get("bank_branch",  "Anna Nagar Main Branch");
            UpiId       = Preferences.Get("upi_id",       "glassworks@sbi");

            TermsConditions = Preferences.Get("terms_conditions",
                "1. Goods once sold will not be taken back or exchanged.\n" +
                "2. All disputes are subject to local jurisdiction only.\n" +
                "3. Payment due within 30 days of invoice date.\n" +
                "4. Breakage during transport is not our responsibility.\n" +
                "5. Please check material carefully at the time of delivery.");
        }
    }

    [RelayCommand]
    private async Task SaveWorkstationCodeAsync()
    {
        var code = WorkstationCodeSettings.Normalize(WorkstationCode);
        if (!WorkstationCodeSettings.IsValid(code))
        {
            await Shell.Current.DisplayAlert(
                "Invalid Workstation Code",
                "Enter one letter from A to Z, and use a different letter on each workstation.",
                "OK");
            return;
        }

        WorkstationCodeSettings.SetCode(code);
        WorkstationCode = code;
        await Shell.Current.DisplayAlert("Saved", "Workstation code saved.", "OK");
    }

    [RelayCommand]
    public async Task LoadThicknessTypesAsync()
    {
        var list = await _db.GetThicknessTypesAsync(activeOnly: false);
        ThicknessTypes.Clear();
        foreach (var t in list) ThicknessTypes.Add(t);
    }

    [RelayCommand]
    public async Task LoadGlassTypesAsync()
    {
        var list = await _db.GetGlassTypesAsync();
        GlassTypes.Clear();
        foreach (var glassType in list) GlassTypes.Add(glassType);
    }

    [RelayCommand]
    public async Task LoadGlassShapesAsync()
    {
        var list = await _db.GetGlassShapesAsync();
        GlassShapes.Clear();
        foreach (var glassShape in list) GlassShapes.Add(glassShape);
    }

    [RelayCommand]
    private async Task SaveCompanyInfoAsync()
    {
        Preferences.Set("company_name",    CompanyName.Trim());
        Preferences.Set("company_address", CompanyAddress.Trim());
        Preferences.Set("company_phone",   CompanyPhone.Trim());
        Preferences.Set("company_email",   CompanyEmail.Trim());
        Preferences.Set("company_gstin",   CompanyGstin.Trim());
        Preferences.Set("default_tax",     DefaultTax.ToString());

        var s = await _db.GetSettingsAsync();
        s.CompanyName = CompanyName.Trim();
        s.CompanyAddress = CompanyAddress.Trim();
        s.CompanyPhone = CompanyPhone.Trim();
        s.CompanyEmail = CompanyEmail.Trim();
        s.CompanyGstin = CompanyGstin.Trim();
        s.DefaultTax = DefaultTax;
        await _db.SaveSettingsAsync(s);

        await Shell.Current.DisplayAlert("Saved", "Company info saved.", "OK");
    }

    [RelayCommand]
    private async Task SaveBankDetailsAsync()
    {
        Preferences.Set("bank_name",    BankName.Trim());
        Preferences.Set("bank_account", BankAccount.Trim());
        Preferences.Set("bank_ifsc",    BankIfsc.Trim());
        Preferences.Set("bank_branch",  BankBranch.Trim());
        Preferences.Set("upi_id",       UpiId.Trim());

        var s = await _db.GetSettingsAsync();
        s.BankName = BankName.Trim();
        s.BankAccount = BankAccount.Trim();
        s.BankIfsc = BankIfsc.Trim();
        s.BankBranch = BankBranch.Trim();
        s.UpiId = UpiId.Trim();
        await _db.SaveSettingsAsync(s);

        await Shell.Current.DisplayAlert("Saved", "Bank details saved.", "OK");
    }

    [RelayCommand]
    private async Task SaveTermsAsync()
    {
        Preferences.Set("terms_conditions", TermsConditions);
        var s = await _db.GetSettingsAsync();
        s.TermsConditions = TermsConditions;
        await _db.SaveSettingsAsync(s);
        await Shell.Current.DisplayAlert("Saved", "Terms & Conditions saved.", "OK");
    }

    [RelayCommand]
    private void EditThickness(ThicknessType t)
    {
        EditingThickness = t;
        EditName  = t.Name;
        EditPrice = t.PricePerSqFt;
        IsEditingThickness = true;
    }

    [RelayCommand]
    private async Task SaveThicknessAsync()
    {
        EditingThickness.Name         = EditName.Trim();
        EditingThickness.PricePerSqFt = EditPrice;
        await _db.SaveThicknessTypeAsync(EditingThickness);
        IsEditingThickness = false;
        await LoadThicknessTypesAsync();
    }

    [RelayCommand]
    private void CancelThicknessEdit() => IsEditingThickness = false;

    [RelayCommand]
    private void EditGlassType(GlassType glassType)
    {
        EditingGlassType = glassType;
        EditGlassTypeName = glassType.Name;
        EditGlassWastageInches = glassType.WastageInches;
        IsEditingGlassType = true;
    }

    [RelayCommand]
    private void AddGlassType()
    {
        EditingGlassType = new GlassType { SortOrder = GlassTypes.Count + 1 };
        EditGlassTypeName = string.Empty;
        EditGlassWastageInches = 2;
        IsEditingGlassType = true;
    }

    [RelayCommand]
    private async Task SaveGlassTypeAsync()
    {
        var name = EditGlassTypeName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            await Shell.Current.DisplayAlert("Validation", "Glass type name is required.", "OK");
            return;
        }

        if (EditGlassWastageInches < 0)
        {
            await Shell.Current.DisplayAlert("Validation", "Wastage cannot be negative.", "OK");
            return;
        }

        if (GlassTypes.Any(t => t.Id != EditingGlassType.Id &&
            string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            await Shell.Current.DisplayAlert("Validation", "That glass type already exists.", "OK");
            return;
        }

        EditingGlassType.Name = name;
        EditingGlassType.WastageInches = EditGlassWastageInches;
        await _db.SaveGlassTypeAsync(EditingGlassType);
        IsEditingGlassType = false;
        await LoadGlassTypesAsync();
    }

    [RelayCommand]
    private void CancelGlassTypeEdit() => IsEditingGlassType = false;

    [RelayCommand]
    private async Task DeleteGlassTypeAsync(GlassType glassType)
    {
        if (GlassTypes.Count <= 1)
        {
            await Shell.Current.DisplayAlert("Cannot Delete", "At least one glass type must remain.", "OK");
            return;
        }

        bool confirm = await Shell.Current.DisplayAlert(
            "Delete Glass Type",
            $"Delete '{glassType.Name}'? Existing bills will not be changed.",
            "Delete",
            "Cancel");
        if (!confirm) return;

        await _db.DeleteGlassTypeAsync(glassType);
        await LoadGlassTypesAsync();
    }

    [RelayCommand]
    private void EditGlassShape(GlassShape glassShape)
    {
        EditingGlassShape = glassShape;
        EditGlassShapeName = glassShape.Name;
        IsEditingGlassShape = true;
    }

    [RelayCommand]
    private void AddGlassShape()
    {
        EditingGlassShape = new GlassShape { SortOrder = GlassShapes.Count + 1 };
        EditGlassShapeName = string.Empty;
        IsEditingGlassShape = true;
    }

    [RelayCommand]
    private async Task SaveGlassShapeAsync()
    {
        var name = EditGlassShapeName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            await Shell.Current.DisplayAlert("Validation", "Glass shape name is required.", "OK");
            return;
        }

        if (GlassShapes.Any(shape => shape.Id != EditingGlassShape.Id &&
            string.Equals(shape.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            await Shell.Current.DisplayAlert("Validation", "That glass shape already exists.", "OK");
            return;
        }

        EditingGlassShape.Name = name;
        await _db.SaveGlassShapeAsync(EditingGlassShape);
        IsEditingGlassShape = false;
        await LoadGlassShapesAsync();
    }

    [RelayCommand]
    private void CancelGlassShapeEdit() => IsEditingGlassShape = false;

    [RelayCommand]
    private async Task DeleteGlassShapeAsync(GlassShape glassShape)
    {
        if (GlassShapes.Count <= 1)
        {
            await Shell.Current.DisplayAlert("Cannot Delete", "At least one glass shape must remain.", "OK");
            return;
        }

        bool confirm = await Shell.Current.DisplayAlert(
            "Delete Glass Shape",
            $"Delete '{glassShape.Name}'? Existing bills will not be changed.",
            "Delete",
            "Cancel");
        if (!confirm) return;

        await _db.DeleteGlassShapeAsync(glassShape);
        await LoadGlassShapesAsync();
    }

    [RelayCommand]
    private async Task AddThicknessTypeAsync()
    {
        string? name = await Shell.Current.DisplayPromptAsync(
            "New Thickness", "Enter thickness name (e.g. 20mm):", "Add", "Cancel");
        if (string.IsNullOrWhiteSpace(name)) return;

        var t = new ThicknessType
        {
            Name           = name.Trim(),
            ThicknessValue = double.TryParse(name.Replace("mm", ""), out var v) ? v : 0,
            IsActive       = true,
            SortOrder      = ThicknessTypes.Count + 1
        };
        await _db.SaveThicknessTypeAsync(t);
        await LoadThicknessTypesAsync();
    }

    [RelayCommand]
    private async Task ToggleActiveAsync(ThicknessType t)
    {
        t.IsActive = !t.IsActive;
        await _db.SaveThicknessTypeAsync(t);
        await LoadThicknessTypesAsync();
    }

    [RelayCommand]
    private async Task BackupDatabaseAsync()
    {
        IsBusy = true;
        try
        {
            var path = await _backup.BackupAsync();
            if (path is not null)
                await Shell.Current.DisplayAlert("Backup Complete", $"Saved to:\n{path}", "OK");
            else
                await Shell.Current.DisplayAlert("Cancelled", "Backup was cancelled.", "OK");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RestoreDatabaseAsync()
    {
        bool confirm = await Shell.Current.DisplayAlert(
            "Restore", "This will replace all current data with the backup. Continue?", "Restore", "Cancel");
        if (!confirm) return;

        IsBusy = true;
        try
        {
            bool ok = await _backup.RestoreAsync();
            await Shell.Current.DisplayAlert(
                ok ? "Restored" : "Error",
                ok ? "Database restored successfully. Restart the app." : "Restore failed.",
                "OK");
        }
        finally { IsBusy = false; }
    }
}

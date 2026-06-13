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
    [ObservableProperty] private string _companyName    = string.Empty;
    [ObservableProperty] private string _companyAddress = string.Empty;
    [ObservableProperty] private string _companyPhone   = string.Empty;
    [ObservableProperty] private string _companyEmail   = string.Empty;
    [ObservableProperty] private double _defaultTax     = 0;

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

    public SettingsViewModel(DatabaseService db, BackupService backup)
    {
        _db     = db;
        _backup = backup;
        Title   = "Settings";
        LoadCompanyInfo();
    }

    private void LoadCompanyInfo()
    {
        CompanyName    = Preferences.Get("company_name",    "Glass Works Pvt. Ltd.");
        CompanyAddress = Preferences.Get("company_address", "123, Glass Street, Industrial Area, City – 600001");
        CompanyPhone   = Preferences.Get("company_phone",   "+91 98765 43210");
        CompanyEmail   = Preferences.Get("company_email",   "info@glassworks.com");
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

    [RelayCommand]
    public async Task LoadThicknessTypesAsync()
    {
        var list = await _db.GetThicknessTypesAsync(activeOnly: false);
        ThicknessTypes.Clear();
        foreach (var t in list) ThicknessTypes.Add(t);
    }

    [RelayCommand]
    private void SaveCompanyInfo()
    {
        Preferences.Set("company_name",    CompanyName.Trim());
        Preferences.Set("company_address", CompanyAddress.Trim());
        Preferences.Set("company_phone",   CompanyPhone.Trim());
        Preferences.Set("company_email",   CompanyEmail.Trim());
        Preferences.Set("default_tax",     DefaultTax.ToString());
        Shell.Current.DisplayAlert("Saved", "Company info saved.", "OK");
    }

    [RelayCommand]
    private void SaveBankDetails()
    {
        Preferences.Set("bank_name",    BankName.Trim());
        Preferences.Set("bank_account", BankAccount.Trim());
        Preferences.Set("bank_ifsc",    BankIfsc.Trim());
        Preferences.Set("bank_branch",  BankBranch.Trim());
        Preferences.Set("upi_id",       UpiId.Trim());
        Shell.Current.DisplayAlert("Saved", "Bank details saved.", "OK");
    }

    [RelayCommand]
    private void SaveTerms()
    {
        Preferences.Set("terms_conditions", TermsConditions);
        Shell.Current.DisplayAlert("Saved", "Terms & Conditions saved.", "OK");
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

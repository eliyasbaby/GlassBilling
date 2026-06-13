using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassBilling.Models;
using GlassBilling.Services;
using GlassBilling.Views;
using System.Collections.ObjectModel;

namespace GlassBilling.ViewModels;

public partial class BillHistoryViewModel : BaseViewModel
{
    private readonly DatabaseService _db;
    private readonly PdfService _pdf;

    [ObservableProperty] private ObservableCollection<Bill> _bills = new();
    [ObservableProperty] private string _searchText = string.Empty;

    public BillHistoryViewModel(DatabaseService db, PdfService pdf)
    {
        _db  = db;
        _pdf = pdf;
        Title = "Bill History";
    }

    [RelayCommand]
    public async Task LoadBillsAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var list = await _db.GetBillsAsync();
            Bills.Clear();
            foreach (var b in list) Bills.Add(b);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ViewBillAsync(Bill bill)
    {
        var full = await _db.GetBillWithDetailsAsync(bill.Id);
        if (full is null) return;
        var navParams = new Dictionary<string, object> { { "Bill", full } };
        await Shell.Current.GoToAsync(nameof(BillPreviewPage), navParams);
    }

    [RelayCommand]
    private async Task DeleteBillAsync(Bill bill)
    {
        bool confirm = await Shell.Current.DisplayAlert(
            "Delete", $"Delete bill {bill.BillNumber}?", "Delete", "Cancel");
        if (!confirm) return;

        await _db.DeleteBillAsync(bill.Id);
        await LoadBillsAsync();
    }

    partial void OnSearchTextChanged(string value) => FilterBills(value);

    private async void FilterBills(string query)
    {
        var all = await _db.GetBillsAsync();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? all
            : all.Where(b =>
                (b.BillNumber?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (b.Customer?.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();

        Bills.Clear();
        foreach (var b in filtered) Bills.Add(b);
    }
}

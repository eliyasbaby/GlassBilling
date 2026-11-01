using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassBilling.Models;
using GlassBilling.Services;
using GlassBilling.Views;
using System.Collections.ObjectModel;

namespace GlassBilling.ViewModels;

public partial class BillHistoryViewModel : BaseViewModel
{
    private const int PageSize = 20;
    private readonly DatabaseService _db;
    private readonly PdfService _pdf;
    private readonly NewBillViewModel _newBillVm;
    private CancellationTokenSource? _searchDebounce;

    [ObservableProperty] private ObservableCollection<Bill> _bills = new();
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;
    [ObservableProperty] private int _totalBillCount;
    [ObservableProperty] private string _pageStatus = "Page 1 of 1";
    [ObservableProperty] private bool _canGoPrevious;
    [ObservableProperty] private bool _canGoNext;

    public BillHistoryViewModel(DatabaseService db, PdfService pdf, NewBillViewModel newBillVm)
    {
        _db  = db;
        _pdf = pdf;
        _newBillVm = newBillVm;
        Title = "Bill History";
    }

    [RelayCommand]
    public async Task LoadBillsAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var result = await _db.GetBillsPageAsync(CurrentPage, PageSize, SearchText);
            TotalBillCount = result.TotalCount;
            TotalPages = Math.Max(1, (TotalBillCount + PageSize - 1) / PageSize);

            if (CurrentPage > TotalPages)
            {
                CurrentPage = TotalPages;
                result = await _db.GetBillsPageAsync(CurrentPage, PageSize, SearchText);
            }

            Bills.Clear();
            foreach (var bill in result.Bills)
                Bills.Add(bill);

            PageStatus = $"Page {CurrentPage} of {TotalPages} ({TotalBillCount} bills)";
            CanGoPrevious = CurrentPage > 1;
            CanGoNext = CurrentPage < TotalPages;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (!CanGoPrevious) return;
        CurrentPage--;
        await LoadBillsAsync();
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (!CanGoNext) return;
        CurrentPage++;
        await LoadBillsAsync();
    }

    [RelayCommand]
    private async Task ViewBillAsync(Bill bill)
    {
        var full = await _db.GetBillWithDetailsAsync(bill.Id);
        if (full is null) return;
        var navParams = new Dictionary<string, object> { { "BillId", full.Id } };
        await Shell.Current.GoToAsync(nameof(BillPreviewPage), navParams);
    }

    [RelayCommand]
    private async Task EditBillAsync(Bill bill)
    {
        var full = await _db.GetBillWithDetailsAsync(bill.Id);
        if (full is null) return;

        await _newBillVm.LoadBillForEditingAsync(full);
        await Shell.Current.GoToAsync($"//{nameof(BillingPage)}");
    }

    [RelayCommand]
    private async Task ToggleConfirmationAsync(Bill bill)
    {
        var updated = await _db.UpdateBillConfirmationAsync(bill.Id, !bill.IsConfirmed);
        if (updated > 0)
            await LoadBillsAsync();
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

    partial void OnSearchTextChanged(string value)
    {
        _searchDebounce?.Cancel();
        _searchDebounce?.Dispose();
        _searchDebounce = new CancellationTokenSource();
        _ = ReloadAfterSearchAsync(_searchDebounce.Token);
    }

    private async Task ReloadAfterSearchAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(300, cancellationToken);
            CurrentPage = 1;
            await LoadBillsAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }
}

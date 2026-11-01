using CommunityToolkit.Mvvm.ComponentModel;
using GlassBilling.Models;
using GlassBilling.Services;
using System.Collections.ObjectModel;

namespace GlassBilling.ViewModels;

public partial class ReportsViewModel : BaseViewModel
{
    private readonly DatabaseService _db;
    private List<Bill> _dateBills = new();
    private CancellationTokenSource? _dateReload;

    [ObservableProperty] private ObservableCollection<Bill> _bills = new();
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedFilter = "Confirmed Only";
    [ObservableProperty] private DateTime _startDate = DateTime.Today;
    [ObservableProperty] private DateTime _endDate = DateTime.Today;
    [ObservableProperty] private double _confirmedRevenue;
    [ObservableProperty] private int _shownBillCount;

    public IReadOnlyList<string> BillFilters { get; } = new[]
    {
        "All Bills",
        "Confirmed Only",
        "Quotes"
    };

    public ReportsViewModel(DatabaseService db)
    {
        _db = db;
        Title = "Reports";
    }

    public async Task LoadReportsAsync()
    {
        if (IsBusy) return;

        IsBusy = true;
        try
        {
            _dateBills = StartDate.Date > EndDate.Date
                ? new List<Bill>()
                : await _db.GetBillsInDateRangeAsync(StartDate, EndDate);
            ConfirmedRevenue = _dateBills
                .Where(bill => bill.IsConfirmed)
                .Sum(bill => bill.TotalAmount);
            ApplyFilters();
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilters();
    partial void OnSelectedFilterChanged(string value) => ApplyFilters();
    partial void OnStartDateChanged(DateTime value) => ScheduleDateReload();
    partial void OnEndDateChanged(DateTime value) => ScheduleDateReload();

    private void ScheduleDateReload()
    {
        _dateReload?.Cancel();
        _dateReload?.Dispose();
        _dateReload = new CancellationTokenSource();
        _ = ReloadAfterDateChangeAsync(_dateReload.Token);
    }

    private async Task ReloadAfterDateChangeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(250, cancellationToken);
            await LoadReportsAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ApplyFilters()
    {
        IEnumerable<Bill> filtered = SelectedFilter switch
        {
            "Confirmed Only" => _dateBills.Where(bill => bill.IsConfirmed),
            "Quotes" => _dateBills.Where(bill => !bill.IsConfirmed),
            _ => _dateBills
        };

        var query = SearchText.Trim();
        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(bill =>
                bill.BillNumber.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (bill.Customer?.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var results = filtered.ToList();
        ShownBillCount = results.Count;
        Bills.Clear();
        foreach (var bill in results)
            Bills.Add(bill);
    }
}
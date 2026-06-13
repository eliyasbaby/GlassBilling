using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GlassBilling.Models;
using GlassBilling.Services;
using System.Collections.ObjectModel;

namespace GlassBilling.ViewModels;

public partial class CustomersViewModel : BaseViewModel
{
    private readonly DatabaseService _db;

    [ObservableProperty] private ObservableCollection<Customer> _customers = new();
    [ObservableProperty] private Customer _selectedCustomer = new();
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _searchText = string.Empty;

    // Form fields
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _location = string.Empty;
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string _address = string.Empty;

    public CustomersViewModel(DatabaseService db)
    {
        _db = db;
        Title = "Customers";
    }

    [RelayCommand]
    public async Task LoadCustomersAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var list = await _db.GetCustomersAsync();
            Customers.Clear();
            foreach (var c in list) Customers.Add(c);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void NewCustomer()
    {
        SelectedCustomer = new Customer();
        Name = Location = Phone = Address = string.Empty;
        IsEditing = true;
    }

    [RelayCommand]
    private void EditCustomer(Customer customer)
    {
        SelectedCustomer = customer;
        Name     = customer.Name;
        Location = customer.Location;
        Phone    = customer.Phone;
        Address  = customer.Address;
        IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveCustomerAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            await Shell.Current.DisplayAlert("Validation", "Customer name is required.", "OK");
            return;
        }

        SelectedCustomer.Name     = Name.Trim();
        SelectedCustomer.Location = Location.Trim();
        SelectedCustomer.Phone    = Phone.Trim();
        SelectedCustomer.Address  = Address.Trim();

        await _db.SaveCustomerAsync(SelectedCustomer);
        IsEditing = false;
        await LoadCustomersAsync();
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;

    [RelayCommand]
    private async Task DeleteCustomerAsync(Customer customer)
    {
        bool confirm = await Shell.Current.DisplayAlert(
            "Delete", $"Delete customer '{customer.Name}'?", "Delete", "Cancel");
        if (!confirm) return;

        await _db.DeleteCustomerAsync(customer);
        await LoadCustomersAsync();
    }

    partial void OnSearchTextChanged(string value) => FilterCustomers(value);

    private async void FilterCustomers(string query)
    {
        var all = await _db.GetCustomersAsync();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? all
            : all.Where(c => c.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                           || c.Location.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        Customers.Clear();
        foreach (var c in filtered) Customers.Add(c);
    }
}

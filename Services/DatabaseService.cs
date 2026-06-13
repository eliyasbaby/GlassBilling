using GlassBilling.Models;
using SQLite;

namespace GlassBilling.Services;

public class DatabaseService
{
    private SQLiteAsyncConnection? _db;

    public string DbPath => Path.Combine(FileSystem.AppDataDirectory, "glassbilling.db3");

    private async Task InitAsync()
    {
        if (_db is not null) return;

        _db = new SQLiteAsyncConnection(DbPath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

        await _db.CreateTableAsync<Customer>();
        await _db.CreateTableAsync<ThicknessType>();
        await _db.CreateTableAsync<Bill>();
        await _db.CreateTableAsync<BillItem>();
        await _db.CreateTableAsync<MeasurementRow>();
        await _db.CreateTableAsync<ExtraCharge>();

        await SeedDefaultDataAsync();
    }

    private async Task SeedDefaultDataAsync()
    {
        if (await _db!.Table<ThicknessType>().CountAsync() == 0)
            await _db.InsertAllAsync(ThicknessType.Defaults);
    }

    // ── Customers ─────────────────────────────────────────────────────────────

    public async Task<List<Customer>> GetCustomersAsync()
    {
        await InitAsync();
        return await _db!.Table<Customer>().OrderBy(c => c.Name).ToListAsync();
    }

    public async Task<Customer?> GetCustomerAsync(int id)
    {
        await InitAsync();
        return await _db!.FindAsync<Customer>(id);
    }

    public async Task<int> SaveCustomerAsync(Customer c)
    {
        await InitAsync();
        return c.Id == 0 ? await _db!.InsertAsync(c) : await _db!.UpdateAsync(c);
    }

    public async Task<int> DeleteCustomerAsync(Customer c)
    {
        await InitAsync();
        return await _db!.DeleteAsync(c);
    }

    // ── Thickness Types ───────────────────────────────────────────────────────

    public async Task<List<ThicknessType>> GetThicknessTypesAsync(bool activeOnly = true)
    {
        await InitAsync();
        var q = _db!.Table<ThicknessType>().OrderBy(t => t.SortOrder);
        return activeOnly ? await q.Where(t => t.IsActive).ToListAsync() : await q.ToListAsync();
    }

    public async Task<int> SaveThicknessTypeAsync(ThicknessType t)
    {
        await InitAsync();
        return t.Id == 0 ? await _db!.InsertAsync(t) : await _db!.UpdateAsync(t);
    }

    public async Task<int> DeleteThicknessTypeAsync(ThicknessType t)
    {
        await InitAsync();
        return await _db!.DeleteAsync(t);
    }

    // ── Bills ─────────────────────────────────────────────────────────────────

    public async Task<List<Bill>> GetBillsAsync()
    {
        await InitAsync();
        var bills = await _db!.Table<Bill>().OrderByDescending(b => b.BillDate).ToListAsync();
        foreach (var bill in bills)
            bill.Customer = await GetCustomerAsync(bill.CustomerId);
        return bills;
    }

    public async Task<Bill?> GetBillWithDetailsAsync(int billId)
    {
        await InitAsync();
        var bill = await _db!.FindAsync<Bill>(billId);
        if (bill is null) return null;

        bill.Customer     = await GetCustomerAsync(bill.CustomerId);
        bill.Items        = await _db.Table<BillItem>().Where(i => i.BillId == billId).ToListAsync();
        bill.ExtraCharges = await _db.Table<ExtraCharge>().Where(e => e.BillId == billId).ToListAsync();

        foreach (var item in bill.Items)
        {
            item.Measurements = await _db.Table<MeasurementRow>()
                .Where(m => m.BillItemId == item.Id)
                .OrderBy(m => m.RowNumber)
                .ToListAsync();
        }

        return bill;
    }

    public async Task<int> SaveBillAsync(Bill bill)
    {
        await InitAsync();

        if (bill.Id == 0)
        {
            var count = await _db!.Table<Bill>().CountAsync();
            bill.BillNumber = $"GB-{DateTime.Now:yyyyMM}-{count + 1:D4}";
            await _db.InsertAsync(bill);
        }
        else
        {
            await _db!.UpdateAsync(bill);
        }

        foreach (var item in bill.Items)
        {
            item.BillId = bill.Id;
            if (item.Id == 0) await _db.InsertAsync(item);
            else await _db.UpdateAsync(item);

            foreach (var row in item.Measurements)
            {
                row.BillItemId = item.Id;
                if (row.Id == 0) await _db.InsertAsync(row);
                else await _db.UpdateAsync(row);
            }
        }

        // Delete old extra charges and re-insert
        await _db.Table<ExtraCharge>().DeleteAsync(e => e.BillId == bill.Id);
        foreach (var ec in bill.ExtraCharges)
        {
            ec.BillId = bill.Id;
            await _db.InsertAsync(ec);
        }

        return bill.Id;
    }

    public async Task DeleteBillAsync(int billId)
    {
        await InitAsync();
        var items = await _db!.Table<BillItem>().Where(i => i.BillId == billId).ToListAsync();
        foreach (var item in items)
            await _db.Table<MeasurementRow>().DeleteAsync(m => m.BillItemId == item.Id);
        await _db.Table<BillItem>().DeleteAsync(i => i.BillId == billId);
        await _db.Table<ExtraCharge>().DeleteAsync(e => e.BillId == billId);
        await _db.DeleteAsync<Bill>(billId);
    }

    public async Task<(int bills, int customers, double revenue)> GetStatsAsync()
    {
        await InitAsync();
        int bills     = await _db!.Table<Bill>().CountAsync();
        int customers = await _db.Table<Customer>().CountAsync();
        double revenue = (await _db.Table<Bill>().ToListAsync()).Sum(b => b.TotalAmount);
        return (bills, customers, revenue);
    }

    public async Task CloseAsync()
    {
        if (_db is not null) { await _db.CloseAsync(); _db = null; }
    }
}

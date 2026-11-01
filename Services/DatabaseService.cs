using GlassBilling.Models;
using SQLite;
using System.Diagnostics;
using System.IO;

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
        await _db.CreateTableAsync<GlassType>();
        await _db.CreateTableAsync<Bill>();
        await _db.CreateTableAsync<BillItem>();
        await _db.CreateTableAsync<MeasurementRow>();
        await _db.CreateTableAsync<ExtraCharge>();
        await _db.CreateTableAsync<GlassBilling.Models.Settings>();

        await MigrateLegacyWorkstationCodeAsync();
        await SeedDefaultDataAsync();
    }

    private async Task MigrateLegacyWorkstationCodeAsync()
    {
        if (!string.IsNullOrWhiteSpace(WorkstationCodeSettings.GetCode()))
            return;

        try
        {
            var legacyCode = await _db!.ExecuteScalarAsync<string>(
                "SELECT WorkstationCode FROM AppSettings WHERE WorkstationCode IS NOT NULL AND TRIM(WorkstationCode) <> '' LIMIT 1");
            if (WorkstationCodeSettings.IsValid(legacyCode))
                WorkstationCodeSettings.SetCode(legacyCode!);
        }
        catch (SQLiteException)
        {
            // Older databases may not have the workstation-code column.
        }
    }

    private async Task SeedDefaultDataAsync()
    {
        if (await _db!.Table<ThicknessType>().CountAsync() == 0)
            await _db.InsertAllAsync(ThicknessType.Defaults);

        if (await _db.Table<GlassType>().CountAsync() == 0)
            await _db.InsertAllAsync(GlassType.Defaults);
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

    // ── Glass Types ──────────────────────────────────────────────────────────

    public async Task<List<GlassType>> GetGlassTypesAsync()
    {
        await InitAsync();
        return await _db!.Table<GlassType>().OrderBy(t => t.SortOrder).ToListAsync();
    }

    public async Task<int> SaveGlassTypeAsync(GlassType glassType)
    {
        await InitAsync();
        return glassType.Id == 0
            ? await _db!.InsertAsync(glassType)
            : await _db!.UpdateAsync(glassType);
    }

    public async Task<int> DeleteGlassTypeAsync(GlassType glassType)
    {
        await InitAsync();
        return await _db!.DeleteAsync(glassType);
    }

    // ── Bills ─────────────────────────────────────────────────────────────────

    public async Task<List<Bill>> GetBillsAsync()
    {
        await InitAsync();
        var bills = await _db!.Table<Bill>().OrderByDescending(b => b.BillDate).ToListAsync();
        await AttachCustomersAsync(bills);
        return bills;
    }

    public async Task<(List<Bill> Bills, int TotalCount)> GetBillsPageAsync(
        int page,
        int pageSize,
        string searchText)
    {
        await InitAsync();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var searchPattern = $"%{searchText.Trim()}%";
        const string filter = "BillNumber LIKE ? OR CustomerId IN (SELECT Id FROM Customers WHERE Name LIKE ?)";

        var totalCount = await _db!.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM Bills WHERE {filter}", searchPattern, searchPattern);
        var bills = await _db.QueryAsync<Bill>(
            $"SELECT * FROM Bills WHERE {filter} ORDER BY BillDate DESC LIMIT ? OFFSET ?",
            searchPattern, searchPattern, pageSize, (page - 1) * pageSize);

        await AttachCustomersAsync(bills);
        return (bills, totalCount);
    }

    public async Task<List<Bill>> GetBillsInDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        await InitAsync();
        var start = startDate.Date;
        var endExclusive = endDate.Date.AddDays(1);
        var bills = await _db!.Table<Bill>()
            .Where(bill => bill.BillDate >= start && bill.BillDate < endExclusive)
            .OrderByDescending(bill => bill.BillDate)
            .ToListAsync();

        await AttachCustomersAsync(bills);
        return bills;
    }

    private async Task AttachCustomersAsync(List<Bill> bills)
    {
        if (bills.Count == 0)
            return;

        var customerIds = bills.Select(bill => bill.CustomerId).Distinct().ToArray();
        var customers = await _db!.Table<Customer>()
            .Where(customer => customerIds.Contains(customer.Id))
            .ToListAsync();
        var customersById = customers.ToDictionary(customer => customer.Id);

        foreach (var bill in bills)
            if (customersById.TryGetValue(bill.CustomerId, out var customer))
                bill.Customer = customer;
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
            var workstationCode = WorkstationCodeSettings.GetCode();
            if (!WorkstationCodeSettings.IsValid(workstationCode))
                throw new InvalidOperationException("Set a valid workstation code in Settings before creating a bill.");

            await _db!.InsertAsync(bill);
            bill.BillNumber = $"TG-PI-{workstationCode}{bill.Id:D4}";
            await _db.UpdateAsync(bill);
        }
        else
        {
            var existingBill = await _db!.FindAsync<Bill>(bill.Id);
            if (!string.IsNullOrWhiteSpace(existingBill?.BillNumber))
                bill.BillNumber = existingBill.BillNumber;

            await _db.UpdateAsync(bill);

            var existingItems = await _db.Table<BillItem>().Where(i => i.BillId == bill.Id).ToListAsync();
            foreach (var item in existingItems)
            {
                await _db.Table<MeasurementRow>().DeleteAsync(m => m.BillItemId == item.Id);
            }
            await _db.Table<BillItem>().DeleteAsync(i => i.BillId == bill.Id);
                // Reset in-memory item Ids so they are re-inserted (avoid Update on deleted rows)
                foreach (var item in bill.Items)
                {
                    item.Id = 0;
                    // also reset measurement ids so they are inserted rather than updated
                    if (item.Measurements is not null)
                    {
                        foreach (var m in item.Measurements)
                            m.Id = 0;
                    }
                }
        }

        Debug.WriteLine($"[DB] Saving Bill id={bill.Id} itemsToSave={bill.Items?.Count ?? 0}");
        // also write to a log file next to the DB for easier capture
        var logPath = DbPath + ".savelog.txt";
        try
        {
            File.AppendAllText(logPath, $"[{DateTime.Now:O}] Saving Bill id={bill.Id} itemsToSave={bill.Items?.Count ?? 0}\n");
        }
        catch { }
        foreach (var item in bill.Items)
        {
            item.BillId = bill.Id;
            if (item.Id == 0)
            {
                await _db.InsertAsync(item);
                Debug.WriteLine($"[DB] Inserted BillItem temp -> new Id={item.Id} thickness={item.ThicknessName}");
                try { File.AppendAllText(logPath, $"[{DateTime.Now:O}] Inserted BillItem new Id={item.Id} thickness={item.ThicknessName}\n"); } catch { }
            }
            else
            {
                await _db.UpdateAsync(item);
                Debug.WriteLine($"[DB] Updated BillItem Id={item.Id}");
            }

            Debug.WriteLine($"[DB] Saving measurements for item.Id={item.Id} count={item.Measurements?.Count ?? 0}");
            try { File.AppendAllText(logPath, $"[{DateTime.Now:O}] Saving measurements for item.Id={item.Id} count={item.Measurements?.Count ?? 0}\n"); } catch { }
            foreach (var row in item.Measurements)
            {
                row.BillItemId = item.Id;
                if (row.Id == 0)
                {
                    await _db.InsertAsync(row);
                    Debug.WriteLine($"[DB] Inserted Measurement Id={row.Id} BillItemId={row.BillItemId} len={row.Length} wid={row.Width}");
                    try { File.AppendAllText(logPath, $"[{DateTime.Now:O}] Inserted Measurement Id={row.Id} BillItemId={row.BillItemId} len={row.Length} wid={row.Width}\n"); } catch { }
                }
                else
                {
                    await _db.UpdateAsync(row);
                    Debug.WriteLine($"[DB] Updated Measurement Id={row.Id} BillItemId={row.BillItemId}");
                    try { File.AppendAllText(logPath, $"[{DateTime.Now:O}] Updated Measurement Id={row.Id} BillItemId={row.BillItemId}\n"); } catch { }
                }
            }
        }

        // Log persisted counts
        try
        {
            var mcount = await _db.Table<MeasurementRow>().Where(m => bill.Items.Select(i => i.Id).Contains(m.BillItemId)).CountAsync();
            Debug.WriteLine($"[DB] After save: total measurement rows for bill {bill.Id} = {mcount}");
        }
        catch { }

        // Delete old extra charges and re-insert
        await _db.Table<ExtraCharge>().DeleteAsync(e => e.BillId == bill.Id);
        foreach (var ec in bill.ExtraCharges)
        {
            ec.BillId = bill.Id;
            await _db.InsertAsync(ec);
        }

        return bill.Id;
    }

    public async Task<int> UpdateBillConfirmationAsync(int billId, bool isConfirmed)
    {
        await InitAsync();
        var bill = await _db!.FindAsync<Bill>(billId);
        if (bill is null) return 0;

        bill.IsConfirmed = isConfirmed;
        return await _db.UpdateAsync(bill);
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
        double revenue = (await _db.Table<Bill>().Where(b => b.IsConfirmed).ToListAsync())
            .Sum(b => b.TotalAmount);
        return (bills, customers, revenue);
    }

    public async Task CloseAsync()
    {
        if (_db is not null) { await _db.CloseAsync(); _db = null; }
    }

    // Settings
    public async Task<GlassBilling.Models.Settings> GetSettingsAsync()
    {
        await InitAsync();
        var s = await _db!.Table<GlassBilling.Models.Settings>().FirstOrDefaultAsync();
        if (s is not null) return s;

        // create default from Preferences if available
        var def = new GlassBilling.Models.Settings
        {
            CompanyName = Preferences.Get("company_name", "Glass Works Pvt. Ltd."),
            CompanyAddress = Preferences.Get("company_address", "123, Glass Street, Industrial Area, City – 600001"),
            CompanyPhone = Preferences.Get("company_phone", "+91 98765 43210"),
            CompanyEmail = Preferences.Get("company_email", "info@glassworks.com"),
            DefaultTax = double.TryParse(Preferences.Get("default_tax", "18"), out var t) ? t : 18,
            BankName = Preferences.Get("bank_name", "State Bank of India"),
            BankAccount = Preferences.Get("bank_account", "1234567890"),
            BankIfsc = Preferences.Get("bank_ifsc", "SBIN0001234"),
            BankBranch = Preferences.Get("bank_branch", ""),
            UpiId = Preferences.Get("upi_id", ""),
            TermsConditions = Preferences.Get("terms_conditions", "")
        };

        await _db.InsertAsync(def);
        return def;
    }

    public async Task<int> SaveSettingsAsync(GlassBilling.Models.Settings s)
    {
        await InitAsync();
        if (s.Id == 0) return await _db!.InsertAsync(s);
        return await _db!.UpdateAsync(s);
    }
}

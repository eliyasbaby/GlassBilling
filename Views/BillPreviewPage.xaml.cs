using GlassBilling.Models;
using GlassBilling.Services;
using GlassBilling.ViewModels;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.Maui.Controls;

namespace GlassBilling.Views;

[QueryProperty(nameof(Bill), "Bill")]
public partial class BillPreviewPage : ContentPage, IQueryAttributable
{
    private readonly PdfService _pdf;
    private readonly DatabaseService _db;
    private readonly NewBillViewModel _newBillVm;
    private Bill? _bill;
    private string? _lastFilePath;
    private bool _pendingGeneration;
    private bool _isEditingBill;
    private bool _isGenerating;

    private static readonly Color Border = Color.FromArgb("#8F8F8F");
    private static readonly Color HeaderFill = Color.FromArgb("#EEEEEE");
    private static readonly Color TextDark = Colors.Black;

    private static readonly double[] ColW = { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };

    public Bill? Bill
    {
        get => _bill;
        set { _bill = value; PopulateBillView(); }
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query == null) return;

        _pendingGeneration = query.TryGetValue("PendingGeneration", out var pendingValue)
            && pendingValue is bool pending && pending;
        _isEditingBill = query.TryGetValue("IsEditingBill", out var editingValue)
            && editingValue is bool isEditing && isEditing;

        if (query.TryGetValue("Bill", out var val) && val is Bill b)
        {
            Bill = b;
            return;
        }

        if (query.TryGetValue("Bill", out var jsonObj) && jsonObj is string json)
        {
            try
            {
                var deserialized = JsonSerializer.Deserialize<Bill>(json);
                if (deserialized is not null) Bill = deserialized;
            }
            catch { /* ignore invalid JSON */ }
        }

        // If caller passed only the BillId, load full details from DB to ensure Items/Measurements are populated
        if (query.TryGetValue("BillId", out var idObj))
        {
            _pendingGeneration = false;
            _isEditingBill = false;
            int id = 0;
            if (idObj is int iid) id = iid;
            else if (idObj is long il) id = (int)il;
            else if (idObj is string s && int.TryParse(s, out var parsed)) id = parsed;

            if (id > 0)
            {
                LoadBillByIdAsync(id);
            }
        }
    }

    private async void LoadBillByIdAsync(int id)
    {
        try
        {
            var bill = await _db.GetBillWithDetailsAsync(id);
            if (bill is not null)
            {
                Debug.WriteLine($"[BillPreview] Loaded bill id={bill.Id}, items={bill.Items?.Count ?? 0}, extras={bill.ExtraCharges?.Count ?? 0}");
                foreach (var it in bill.Items)
                {
                    Debug.WriteLine($"[BillPreview] Item id={it.Id} thickness='{it.ThicknessName}' glass='{it.GlassDescription}' unit='{it.MeasurementUnit}' price={it.PricePerSqFt} measurements={it.Measurements?.Count ?? 0}");
                    foreach (var m in it.Measurements)
                        Debug.WriteLine($"[BillPreview]  Row id={m.Id} len={m.Length} wid={m.Width} chargeArea={m.ChargeAreaSqFt}");
                }

                Bill = bill;
            }
        }
        catch
        {
            // ignore
        }
    }

    public BillPreviewPage(PdfService pdf, DatabaseService db, NewBillViewModel newBillVm)
    {
        InitializeComponent();
        _pdf = pdf;
        _db = db;
        _newBillVm = newBillVm;
    }

    private async void PopulateBillView()
    {
        if (_bill is null) return;
        GenerateBillButton.Text = _pendingGeneration
            ? _isEditingBill ? "Update Bill" : "Generate Bill"
            : "Generate PDF";
        GenerateBillButton.IsEnabled = !_isGenerating;

        // If Items are empty, ensure we load full details from DB (some navigation paths pass partial objects)
        if ((_bill.Items == null || _bill.Items.Count == 0) && _bill.Id > 0)
        {
            try
            {
                var fresh = await _db.GetBillWithDetailsAsync(_bill.Id);
                if (fresh is not null) _bill = fresh;
            }
            catch { }
        }

        var s = await _db.GetSettingsAsync();

        CompanyLabel.Text = s.CompanyName;
        CompanyAddressLabel.Text = s.CompanyAddress;
        CompanyPhoneLabel.Text = string.IsNullOrWhiteSpace(s.CompanyPhone) ? "" : $"PH: {s.CompanyPhone}";
        OfficePhoneLabel.Text = string.IsNullOrWhiteSpace(s.CompanyPhone) ? "Office:" : $"Office: {s.CompanyPhone}";
        CompanyEmailValueLabel.Text = string.IsNullOrWhiteSpace(s.CompanyEmail) ? "E-mail:" : $"E-mail: {s.CompanyEmail}";
        CompanyGstinValueLabel.Text = string.IsNullOrWhiteSpace(s.CompanyGstin) ? "GSTIN:" : $"GSTIN: {s.CompanyGstin}";
        

        CustomerNameLabel.Text = $"Name: {_bill.Customer?.Name ?? ""}";
        CustomerLocationLabel.Text = $"Location: {_bill.Customer?.Location ?? ""}";
        CustomerPhoneLabel.Text = $"Phone: {_bill.Customer?.Phone ?? ""}";

        BillDateLabel.Text = $"Date : {_bill.BillDate:dd-MM-yyyy}";
        BillNumberLabel.Text = string.IsNullOrWhiteSpace(_bill.BillNumber)
            ? "PI No: Assigned when generated"
            : $"PI No: {_bill.BillNumber}";
        SalesmanLabel.Text = $"Salesman: {_bill.SalesmanName}";

        BuildMeasurementTable();
        BuildBankAndTerms(s);
        BuildExtraCharges();
        BuildTotals();

        if (!string.IsNullOrWhiteSpace(_bill.Notes))
        {
            NotesLabel.Text = _bill.Notes;
            NotesFrame.IsVisible = true;
        }
        else
        {
            NotesFrame.IsVisible = false;
        }
    }

    private void BuildMeasurementTable()
    {
        if (_bill is null) return;

        TableContainer.Children.Clear();

        foreach (var item in _bill.Items)
        {
            TableContainer.Children.Add(BuildItemTitle(item));
            TableContainer.Children.Add(BuildTableHeader());

            int sr = 1;
            foreach (var row in item.Measurements)
            {
                TableContainer.Children.Add(BuildMeasurementRow(row, sr, item));
                sr++;
            }

            TableContainer.Children.Add(BuildSubTotalRow(item));
        }
    }

    private Grid BuildItemTitle(BillItem item)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(150))
            }
        };

        grid.Add(Label($"{item.ThicknessName} {item.GlassDescription}", 16, true), 0);
        grid.Add(Label($"Unit:({item.MeasurementUnit})", 16, false, TextAlignment.End), 1);
        return grid;
    }

    private Grid BuildTableHeader()
    {
        var grid = GridWithColumns();
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        grid.Add(Cell("Sl No.", 0, 0, 2, true, fill: HeaderFill));
        grid.Add(Cell("Actual size", 1, 0, 1, true, 2, HeaderFill));
        grid.Add(Cell("Chargeable size", 3, 0, 1, true, 2, HeaderFill));
        grid.Add(Cell("Qty", 5, 0, 2, true, fill: HeaderFill));
        grid.Add(Cell("Chargeable Area", 6, 0, 2, true, fill: HeaderFill));
        grid.Add(Cell("Rate\n(\u20B9/sqft)", 7, 0, 2, true, fill: HeaderFill));
        grid.Add(Cell("Shape", 8, 0, 2, true, fill: HeaderFill));
        grid.Add(Cell("Amount\n(\u20B9)", 9, 0, 2, true, fill: HeaderFill));

        grid.Add(Cell("Height", 1, 1, bold: true, fill: HeaderFill));
        grid.Add(Cell("Width", 2, 1, bold: true, fill: HeaderFill));
        grid.Add(Cell("Height", 3, 1, bold: true, fill: HeaderFill));
        grid.Add(Cell("Width", 4, 1, bold: true, fill: HeaderFill));
        return grid;
    }

    private Grid BuildMeasurementRow(MeasurementRow row, int sr, BillItem item)
    {
        var grid = GridWithColumns();
        var amount = row.ChargeAreaSqFt * item.PricePerSqFt;

        grid.Add(Cell(sr.ToString(), 0, 0));
        grid.Add(Cell(row.Length.ToString("N2"), 1, 0));
        grid.Add(Cell(row.Width.ToString("N2"), 2, 0));
        grid.Add(Cell(row.ChargeLength.ToString("N2"), 3, 0));
        grid.Add(Cell(row.ChargeWidth.ToString("N2"), 4, 0));
        grid.Add(Cell(row.Quantity.ToString(), 5, 0));
        grid.Add(Cell(row.ChargeAreaSqFt.ToString("N2"), 6, 0));
        grid.Add(RateCell(item), 7, 0);
        grid.Add(Cell(item.Shape, 8, 0));
        grid.Add(Cell(amount.ToString("N2"), 9, 0, align: TextAlignment.End));
        return grid;
    }

    private Grid BuildSubTotalRow(BillItem item)
    {
        var grid = GridWithColumns();
        grid.Add(Cell("Totals:", 0, 0, bold: true, align: TextAlignment.End, columnSpan: 5, fill: HeaderFill));
        grid.Add(Cell(item.Measurements.Sum(row => row.Quantity).ToString(), 5, 0, bold: true, fill: HeaderFill));
        grid.Add(Cell(item.TotalChargeAreaSqFt.ToString("N2"), 6, 0));
        grid.Add(Cell("", 7, 0));
        grid.Add(Cell("", 8, 0));
        grid.Add(Cell(item.TotalAmount.ToString("N2"), 9, 0, align: TextAlignment.End));
        return grid;
    }

    private Grid GridWithColumns()
    {
        var tableWidth = GetResponsiveTableWidth();
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(
                ColW.Select(_ => new ColumnDefinition(new GridLength(1, GridUnitType.Star))).ToArray()),
            WidthRequest = tableWidth,
            HorizontalOptions = LayoutOptions.Start
        };
    }

    private double GetResponsiveTableWidth()
    {
        var displayWidth = DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
        var availableWidth = Math.Max(320, displayWidth - 140);
        return Math.Min(744, availableWidth);
    }

    private static Label Label(string text, double size, bool bold = false,
        TextAlignment align = TextAlignment.Start) => new()
    {
        Text = text,
        FontSize = size,
        FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
        TextColor = TextDark,
        HorizontalTextAlignment = align,
        VerticalTextAlignment = TextAlignment.Center
    };

    private static Border Cell(string text, int column, int row, int rowSpan = 1,
        bool bold = false, int columnSpan = 1, Color? fill = null,
        TextAlignment align = TextAlignment.Center)
    {
        var border = new Border
        {
            Stroke = Border,
            StrokeThickness = 0.6,
            BackgroundColor = fill ?? Colors.White,
            Padding = new Thickness(5, 3),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Content = new Label
            {
                Text = text,
                FontSize = 14,
                FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
                TextColor = TextDark,
                HorizontalTextAlignment = align,
                VerticalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.WordWrap
            }
        };

        Grid.SetColumn(border, column);
        Grid.SetRow(border, row);
        Grid.SetRowSpan(border, rowSpan);
        Grid.SetColumnSpan(border, columnSpan);
        return border;
    }

    private Border RateCell(BillItem item)
    {
        return new Border
        {
            Stroke = Border,
            StrokeThickness = 0.6,
            Padding = new Thickness(3, 1),
            BackgroundColor = Colors.White,
            Content = new Label
            {
                Text = item.PricePerSqFt.ToString("N4"),
                FontSize = 13,
                TextColor = TextDark,
                HorizontalTextAlignment = TextAlignment.End,
                VerticalTextAlignment = TextAlignment.Center
            }
        };
    }

    private void BuildBankAndTerms(GlassBilling.Models.Settings s)
    {
        BankLayout.Children.Clear();

        AddBankLine("Account Number:", s.BankAccount);
        AddBankLine("IFSC Code:", s.BankIfsc);
        AddBankLine("Bank:", s.BankName);
        AddBankLine("Branch:", s.BankBranch);
        AddBankLine("UPI ID:", s.UpiId);

        TermsLabel.Text = string.IsNullOrWhiteSpace(s.TermsConditions)
            ? "1. Please check the size, specification, and rate before confirmation.\n2. 60% advance along with the purchase order (payment mode: bank or cash).\n3. Balance to be paid before delivery.\n4. We are not responsible for any damage or breakage after delivery.\n5. Site unloading should be done by the customer."
            : s.TermsConditions;
    }

    private void AddBankLine(string label, string value)
    {
        var line = new HorizontalStackLayout { Spacing = 3 };
        line.Children.Add(Label(label, 12, true));
        line.Children.Add(Label(value, 12));
        BankLayout.Children.Add(line);
    }

    private void BuildExtraCharges()
    {
        if (_bill is null) return;

        ExtraChargesLayout.Children.Clear();
        ExtraChargesLayout.Children.Add(Label("Extra Charges", 21, true));

        if (_bill.ExtraCharges.Count == 0)
        {
            ExtraChargesLayout.Children.Add(Label("No extra charges", 13));
            return;
        }

        foreach (var charge in _bill.ExtraCharges)
        {
            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(new GridLength(132))
                },
                WidthRequest = 263
            };
            grid.Add(Cell(charge.Name, 0, 0, bold: true), 0);
            grid.Add(Cell(charge.Amount.ToString("N1"), 1, 0), 1);
            ExtraChargesLayout.Children.Add(grid);
        }
    }

    private void BuildTotals()
    {
        if (_bill is null) return;

        TotalsLayout.Children.Clear();
        TotalsLayout.Children.Add(Label("Total Calculation", 21, true));

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(132)),
                new ColumnDefinition(new GridLength(132))
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            WidthRequest = 264
        };

        grid.Add(Cell("Total (\u20B9)", 0, 0, bold: true, align: TextAlignment.End), 0, 0);
        grid.Add(Cell(_bill.SubTotal.ToString("N2"), 1, 0), 1, 0);
        grid.Add(Cell($"Tax ({_bill.TaxPercent:N0}%)", 0, 1, bold: true, align: TextAlignment.End), 0, 1);
        grid.Add(Cell(_bill.TaxAmount.ToString("N2"), 1, 1), 1, 1);
        grid.Add(Cell("Discount (\u20B9)", 0, 2, bold: true, align: TextAlignment.End), 0, 2);
        grid.Add(Cell(_bill.DiscountAmount.ToString("N2"), 1, 2), 1, 2);
        grid.Add(Cell("Grand Total (\u20B9)", 0, 3, bold: true, align: TextAlignment.End), 0, 3);
        grid.Add(Cell(_bill.TotalAmount.ToString("N2"), 1, 3, bold: true), 1, 3);
        TotalsLayout.Children.Add(grid);
    }

    private async Task<bool> EnsureBillIsGeneratedAsync()
    {
        if (_bill is null)
            return false;

        if (_pendingGeneration || _bill.Id == 0)
        {
            await DisplayAlert(
                "Generate Bill First",
                "Generate and save the bill before sharing its PDF.",
                "OK");
            return false;
        }

        return true;
    }

    private async void OnGenerateBillClicked(object sender, EventArgs e)
    {
        var bill = _bill;
        if (bill is null || _isGenerating)
            return;

        var savingNewBill = _pendingGeneration;
        var billWasSaved = false;
        _isGenerating = true;
        GenerateBillButton.IsEnabled = false;
        LoadingMessage.Text = savingNewBill
            ? _isEditingBill ? "Updating bill..." : "Generating bill..."
            : "Generating PDF...";
        LoadingOverlay.IsVisible = true;
        try
        {
            if (savingNewBill)
            {
                await _db.SaveBillAsync(bill);
                billWasSaved = true;
                _pendingGeneration = false;
                GenerateBillButton.Text = "Generate PDF";
                BillNumberLabel.Text = $"PI No: {bill.BillNumber}";
                await _newBillVm.LoadStep1Async();
            }

            LoadingMessage.Text = "Generating PDF...";
            _lastFilePath = await _pdf.GenerateBillAsync(bill, "", "", "");
            await _pdf.OpenBillAsync(_lastFilePath);
        }
        catch (Exception ex)
        {
            var message = savingNewBill && !_pendingGeneration
                ? $"Order {bill.BillNumber} was {( _isEditingBill ? "updated" : "saved" )}, but its PDF could not be opened: {ex.Message}"
                : $"Could not generate bill: {ex.Message}";
            await DisplayAlert("Error", message, "OK");
        }
        finally
        {
            _isGenerating = false;
            GenerateBillButton.IsEnabled = true;
            LoadingOverlay.IsVisible = false;
            LoadingMessage.Text = "Generating PDF...";
        }

        if (billWasSaved)
            await Shell.Current.GoToAsync($"//{nameof(BillHistoryPage)}");
    }

    private async void OnShareClicked(object sender, EventArgs e)
    {
        var bill = _bill;
        if (bill is null || !await EnsureBillIsGeneratedAsync()) return;

        var filePath = _lastFilePath;
        if (filePath is null)
        {
            LoadingOverlay.IsVisible = true;
            try
            {
                LoadingMessage.Text = "Generating PDF...";
                filePath = await _pdf.GenerateBillAsync(bill, "", "", "");
                _lastFilePath = filePath;
            }
            finally
            {
                LoadingOverlay.IsVisible = false;
            }
        }

        if (filePath is not null)
            await _pdf.ShareBillAsync(filePath, bill.BillNumber ?? "");
    }
}

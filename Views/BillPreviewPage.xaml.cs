using GlassBilling.Models;
using GlassBilling.Services;

namespace GlassBilling.Views;

[QueryProperty(nameof(Bill), "Bill")]
public partial class BillPreviewPage : ContentPage
{
    private readonly PdfService _pdf;
    private readonly DatabaseService _db;
    private Bill? _bill;
    private string? _lastFilePath;

    private static readonly Color Border = Color.FromArgb("#8F8F8F");
    private static readonly Color HeaderFill = Color.FromArgb("#EEEEEE");
    private static readonly Color TextDark = Colors.Black;

    private static readonly double[] ColW = { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };

    public Bill? Bill
    {
        get => _bill;
        set { _bill = value; PopulateBillView(); }
    }

    public BillPreviewPage(PdfService pdf, DatabaseService db)
    {
        InitializeComponent();
        _pdf = pdf;
        _db = db;
    }

    private async void PopulateBillView()
    {
        if (_bill is null) return;

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
        BillNumberLabel.Text = $"PI No: {_bill.BillNumber}";

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
        grid.Add(Cell("", 5, 0, fill: HeaderFill));
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
        return Math.Min(780, availableWidth);
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
        var entry = new Entry
        {
            Text = item.PricePerSqFt.ToString("N3"),
            Keyboard = Keyboard.Numeric,
            FontSize = 13,
            TextColor = TextDark,
            BackgroundColor = Color.FromArgb("#E7E7E7"),
            HeightRequest = 26,
            Margin = new Thickness(0),
            HorizontalTextAlignment = TextAlignment.End
        };

        entry.Completed += (s, e) => OnRateChanged(entry, item);
        entry.Unfocused += (s, e) => OnRateChanged(entry, item);

        return new Border
        {
            Stroke = Border,
            StrokeThickness = 0.6,
            Padding = new Thickness(3, 1),
            BackgroundColor = Colors.White,
            Content = entry
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
        grid.Add(Cell("Grand Total (\u20B9)", 0, 2, bold: true, align: TextAlignment.End), 0, 2);
        grid.Add(Cell(_bill.TotalAmount.ToString("N2"), 1, 2, bold: true), 1, 2);
        TotalsLayout.Children.Add(grid);
    }

    private void OnRateChanged(Entry entry, BillItem item)
    {
        if (_bill is null || !double.TryParse(entry.Text, out var rate) || Math.Abs(rate - item.PricePerSqFt) < 0.001)
            return;

        item.PricePerSqFt = rate;
        item.TotalAmount = item.TotalChargeAreaSqFt * rate;
        _bill.Recalculate();
        PopulateBillView();
    }

    private async void OnGeneratePdfClicked(object sender, EventArgs e)
    {
        if (_bill is null) return;
        LoadingOverlay.IsVisible = true;
        try
        {
            _lastFilePath = await _pdf.GenerateBillAsync(_bill, "", "", "");
            await _pdf.OpenBillAsync(_lastFilePath);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not generate bill: {ex.Message}", "OK");
        }
        finally
        {
            LoadingOverlay.IsVisible = false;
        }
    }

    private async void OnShareClicked(object sender, EventArgs e)
    {
        if (_bill is null) return;

        if (_lastFilePath is null)
        {
            LoadingOverlay.IsVisible = true;
            try
            {
                _lastFilePath = await _pdf.GenerateBillAsync(_bill, "", "", "");
            }
            finally
            {
                LoadingOverlay.IsVisible = false;
            }
        }

        await _pdf.ShareBillAsync(_lastFilePath, _bill.BillNumber ?? "");
    }
}

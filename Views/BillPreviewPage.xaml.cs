using GlassBilling.Models;
using GlassBilling.Services;

namespace GlassBilling.Views;

[QueryProperty(nameof(Bill), "Bill")]
public partial class BillPreviewPage : ContentPage
{
    private readonly PdfService _pdf;
    private Bill? _bill;
    private string? _lastFilePath;

    // ── Colours (keep in one place) ──────────────────────────────────────────
    private static readonly Color Purple      = Color.FromArgb("#5B4FCF");
    private static readonly Color PurpleLight = Color.FromArgb("#EDE9FF");
    private static readonly Color PurpleDark  = Color.FromArgb("#6A1FCA");
    private static readonly Color RowAlt      = Color.FromArgb("#F8F6FF");
    private static readonly Color TextDark    = Color.FromArgb("#212121");
    private static readonly Color TextGrey    = Color.FromArgb("#757575");
    private static readonly Color Border      = Color.FromArgb("#E0E0E0");

    // ── Column widths for the measurements table ─────────────────────────────
    //   Sr# | Description+Shape | Act L | Act W | Chg L | Chg W | Qty | Sq.ft | Rate | Amt
    private static readonly double[] ColW = { 30, 150, 65, 65, 65, 65, 36, 72, 80, 90 };

    public Bill? Bill
    {
        get => _bill;
        set { _bill = value; PopulateBillView(); }
    }

    public BillPreviewPage(PdfService pdf)
    {
        InitializeComponent();
        _pdf = pdf;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  MAIN POPULATE
    // ════════════════════════════════════════════════════════════════════════
    private void PopulateBillView()
    {
        if (_bill is null) return;

        // ── Company header ────────────────────────────────────────────────
        var cn = Preferences.Get("company_name",    "Glass Works Pvt. Ltd.");
        var ca = Preferences.Get("company_address", "123, Glass Street, City – 600001");
        var cp = Preferences.Get("company_phone",   "+91 98765 43210");
        var ce = Preferences.Get("company_email",   "info@glassworks.com");

        CompanyLabel.Text        = cn;
        CompanyAddressLabel.Text = ca;
        CompanyContactLabel.Text = string.Join("   ",
            new[] { (cp.Length > 0 ? $"📞 {cp}" : ""), (ce.Length > 0 ? ce : "") }
            .Where(s => s.Length > 0));

        BillNumberLabel.Text = $"Bill No: {_bill.BillNumber}";
        BillDateLabel.Text   = $"Date: {_bill.FormattedDate}";

        // ── Customer ──────────────────────────────────────────────────────
        CustomerNameLabel.Text     = _bill.Customer?.Name ?? "";
        CustomerLocationLabel.Text = _bill.Customer?.Location ?? "";
        CustomerPhoneLabel.Text    = _bill.Customer?.Phone is { Length: > 0 } ph ? $"📞 {ph}" : "";

        var upi = Preferences.Get("upi_id", "glassworks@sbi");
        QrUpiLabel.Text = upi;

        // ── Measurements table ────────────────────────────────────────────
        BuildMeasurementTable();

        // ── Extra charges ─────────────────────────────────────────────────
        BuildExtraChargesTable();

        // ── Totals ────────────────────────────────────────────────────────
        SubTotalLabel.Text          = $"₹ {_bill.SubTotal:N2}";
        ExtraChargesTotalLabel.Text = $"₹ {_bill.ExtraChargesTotal:N2}";
        ExtraRow.IsVisible          = _bill.ExtraChargesTotal > 0;
        TaxPercentLabel.Text        = $"GST / Tax  ({_bill.TaxPercent:N0}%)";
        TaxAmountLabel.Text         = $"₹ {_bill.TaxAmount:N2}";
        TotalLabel.Text             = _bill.FormattedTotal;

        // ── Bank details ──────────────────────────────────────────────────
        BankNameLabel.Text    = $"Bank:    {Preferences.Get("bank_name",    "State Bank of India")}";
        BankAccountLabel.Text = $"A/C No:  {Preferences.Get("bank_account", "1234567890")}";
        BankIfscLabel.Text    = $"IFSC:    {Preferences.Get("bank_ifsc",    "SBIN0001234")}";
        BankBranchLabel.Text  = $"Branch:  {Preferences.Get("bank_branch",  "Anna Nagar Main Branch")}";
        UpiIdLabel.Text       = $"UPI:     {Preferences.Get("upi_id",       "glassworks@sbi")}";

        // ── Terms ─────────────────────────────────────────────────────────
        TermsLabel.Text = Preferences.Get("terms_conditions",
            "1. Goods once sold will not be taken back or exchanged.\n" +
            "2. All disputes are subject to local jurisdiction only.\n" +
            "3. Payment due within 30 days of invoice date.\n" +
            "4. Breakage during transport is not our responsibility.\n" +
            "5. Please check material carefully at the time of delivery.");

        // ── Notes ─────────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(_bill.Notes))
        {
            NotesLabel.Text      = _bill.Notes;
            NotesFrame.IsVisible = true;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  MEASUREMENTS TABLE
    // ════════════════════════════════════════════════════════════════════════
    private void BuildMeasurementTable()
    {
        TableContainer.Children.Clear();

        // Column header row
        TableContainer.Children.Add(BuildTableHeaderRow());

        int globalSr = 1;
        foreach (var item in _bill.Items)
        {
            // Thickness section header (spans full width)
            TableContainer.Children.Add(BuildSectionHeader(item));

            // Measurement data rows
            foreach (var m in item.Measurements)
            {
                TableContainer.Children.Add(BuildDataRow(m, globalSr, item));
                globalSr++;
            }

            // Per-thickness subtotal row
            TableContainer.Children.Add(BuildSubTotalRow(item));
        }
    }

    private Grid BuildColDefs() => new Grid
    {
        ColumnDefinitions = new ColumnDefinitionCollection(
            ColW.Select(w => new ColumnDefinition(new GridLength(w))).ToArray())
    };

    private Grid BuildTableHeaderRow()
    {
        var g = BuildColDefs();
        g.BackgroundColor = PurpleDark;
        g.Padding = new Thickness(0, 7);

        string[] headers = { "Sr#", "Description / Shape", "Act. L", "Act. W", "Chg. L", "Chg. W", "Qty", "Sq.ft", "Rate ₹\n/sq.ft", "Amount ₹" };
        for (int i = 0; i < headers.Length; i++)
        {
            g.Add(new Label
            {
                Text                  = headers[i],
                FontSize              = 10,
                FontAttributes        = FontAttributes.Bold,
                TextColor             = Colors.White,
                HorizontalOptions     = LayoutOptions.Center,
                VerticalOptions       = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin                = new Thickness(2, 0)
            }, column: i);
        }
        return g;
    }

    private Grid BuildSectionHeader(BillItem item)
    {
        var total = ColW.Sum();
        var g = new Grid
        {
            BackgroundColor = PurpleLight,
            Padding         = new Thickness(10, 6),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(150))
            },
            MinimumWidthRequest = total
        };

        g.Add(new Label
        {
            Text            = $"{item.ThicknessName}  ·  {item.GlassDescription}  ·  Shape: {item.Shape}  ·  Cutting Allowance: {item.CuttingAllowance}",
            FontSize        = 11,
            FontAttributes  = FontAttributes.Bold,
            TextColor       = Purple,
            VerticalOptions = LayoutOptions.Center
        });

        // Editable rate
        var rateStack = new HorizontalStackLayout { VerticalOptions = LayoutOptions.Center, Spacing = 4 };
        rateStack.Children.Add(new Label
        {
            Text           = "Rate: ₹",
            FontSize       = 11,
            TextColor      = Purple,
            VerticalOptions = LayoutOptions.Center
        });
        var rateEntry = new Entry
        {
            Text                    = item.PricePerSqFt.ToString("N2"),
            Keyboard                = Keyboard.Numeric,
            FontSize                = 12,
            FontAttributes          = FontAttributes.Bold,
            TextColor               = TextDark,
            BackgroundColor         = Colors.White,
            WidthRequest            = 80,
            HeightRequest           = 34,
            HorizontalTextAlignment = TextAlignment.End
        };
        rateEntry.Completed += (s, e) => OnRateChanged(rateEntry, item);
        rateEntry.Unfocused += (s, e) => OnRateChanged(rateEntry, item);
        rateStack.Children.Add(rateEntry);
        rateStack.Children.Add(new Label { Text = "/sq.ft", FontSize = 10, TextColor = Purple, VerticalOptions = LayoutOptions.Center });
        g.Add(rateStack, column: 1);

        return g;
    }

    private Grid BuildDataRow(MeasurementRow m, int sr, BillItem item)
    {
        var g = BuildColDefs();
        g.BackgroundColor = sr % 2 == 0 ? RowAlt : Colors.White;
        g.Padding         = new Thickness(0, 5);

        Label C(string text, bool bold = false, TextAlignment align = TextAlignment.Center) => new Label
        {
            Text                    = text,
            FontSize                = 11,
            FontAttributes          = bold ? FontAttributes.Bold : FontAttributes.None,
            TextColor               = TextDark,
            HorizontalOptions       = LayoutOptions.Fill,
            VerticalOptions         = LayoutOptions.Center,
            HorizontalTextAlignment = align,
            Margin                  = new Thickness(3, 0)
        };

        // Description + Shape on 2 lines
        var descStack = new VerticalStackLayout { Spacing = 1, VerticalOptions = LayoutOptions.Center, Margin = new Thickness(4, 0) };
        descStack.Children.Add(new Label { Text = item.ThicknessName, FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Purple });
        descStack.Children.Add(new Label { Text = item.Shape, FontSize = 9, TextColor = TextGrey });

        g.Add(C(sr.ToString()), column: 0);
        g.Add(descStack,         column: 1);
        g.Add(C($"{m.Length:G}"), column: 2);
        g.Add(C($"{m.Width:G}"),  column: 3);
        g.Add(C($"{m.ChargeLength:G}"), column: 4);
        g.Add(C($"{m.ChargeWidth:G}"),  column: 5);
        g.Add(C($"{m.Quantity}"),        column: 6);
        g.Add(C($"{m.ChargeAreaSqFt:N4}", bold: true), column: 7);

        // Rate: show static text (editable is in section header)
        g.Add(C($"{item.PricePerSqFt:N2}"), column: 8);

        double rowAmt = m.ChargeAreaSqFt * item.PricePerSqFt;
        g.Add(C($"₹ {rowAmt:N2}", bold: true, align: TextAlignment.End), column: 9);

        return g;
    }

    private Grid BuildSubTotalRow(BillItem item)
    {
        var total = ColW.Sum();
        var g = new Grid
        {
            BackgroundColor = Purple,
            Padding         = new Thickness(10, 6),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(200))
            },
            MinimumWidthRequest = total
        };

        g.Add(new Label
        {
            Text      = $"Total Charge Area: {item.TotalChargeAreaSqFt:N4} sq.ft",
            FontSize  = 11,
            TextColor = Colors.White,
            VerticalOptions = LayoutOptions.Center
        });
        g.Add(new Label
        {
            Text              = $"Amount:  ₹ {item.TotalAmount:N2}",
            FontSize          = 12,
            FontAttributes    = FontAttributes.Bold,
            TextColor         = Colors.White,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions   = LayoutOptions.Center
        }, column: 1);

        return g;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  EXTRA CHARGES TABLE
    // ════════════════════════════════════════════════════════════════════════
    private void BuildExtraChargesTable()
    {
        ExtraChargesLayout.Children.Clear();

        if (_bill.ExtraCharges.Count == 0)
        {
            ExtraChargesFrame.IsVisible = false;
            return;
        }
        ExtraChargesFrame.IsVisible = true;

        // Section title bar
        ExtraChargesLayout.Children.Add(new Grid
        {
            BackgroundColor = PurpleDark,
            Padding         = new Thickness(10, 7),
            Children        =
            {
                new Label { Text = "Additional Charges", FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Colors.White }
            }
        });

        // Header row
        var hdr = new Grid
        {
            BackgroundColor = PurpleLight,
            Padding         = new Thickness(10, 5),
            ColumnDefinitions = { new ColumnDefinition(new GridLength(30)), new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(100)) }
        };
        hdr.Add(new Label { Text = "#",           FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Purple, HorizontalOptions = LayoutOptions.Center });
        hdr.Add(new Label { Text = "Description", FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Purple }, column: 1);
        hdr.Add(new Label { Text = "Amount",      FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Purple, HorizontalOptions = LayoutOptions.End }, column: 2);
        ExtraChargesLayout.Children.Add(hdr);

        // Data rows
        int idx = 1;
        foreach (var ec in _bill.ExtraCharges)
        {
            var row = new Grid
            {
                BackgroundColor = idx % 2 == 0 ? RowAlt : Colors.White,
                Padding         = new Thickness(10, 7),
                ColumnDefinitions = { new ColumnDefinition(new GridLength(30)), new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(100)) }
            };
            row.Add(new Label { Text = $"{idx}.", FontSize = 12, TextColor = TextGrey, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center });
            row.Add(new Label { Text = ec.Name,   FontSize = 13, TextColor = TextDark, VerticalOptions = LayoutOptions.Center }, column: 1);
            row.Add(new Label { Text = $"₹ {ec.Amount:N2}", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = TextDark, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center }, column: 2);
            ExtraChargesLayout.Children.Add(row);
            idx++;
        }

        // Total row
        var tot = new Grid
        {
            BackgroundColor = Purple,
            Padding         = new Thickness(10, 7),
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(100)) }
        };
        tot.Add(new Label { Text = "Total Additional Charges", FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, VerticalOptions = LayoutOptions.Center });
        tot.Add(new Label { Text = $"₹ {_bill.ExtraChargesTotal:N2}", FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center }, column: 1);
        ExtraChargesLayout.Children.Add(tot);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  RATE CHANGE → recalc & refresh
    // ════════════════════════════════════════════════════════════════════════
    private void OnRateChanged(Entry entry, BillItem item)
    {
        if (!double.TryParse(entry.Text, out double rate) || rate == item.PricePerSqFt) return;
        item.PricePerSqFt = rate;
        item.TotalAmount  = item.TotalChargeAreaSqFt * rate;
        _bill!.Recalculate();
        PopulateBillView();          // full refresh to update all amounts
    }

    // ════════════════════════════════════════════════════════════════════════
    //  PDF / SHARE
    // ════════════════════════════════════════════════════════════════════════
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
        finally { LoadingOverlay.IsVisible = false; }
    }

    private async void OnShareClicked(object sender, EventArgs e)
    {
        if (_bill is null) return;
        if (_lastFilePath is null)
        {
            LoadingOverlay.IsVisible = true;
            try   { _lastFilePath = await _pdf.GenerateBillAsync(_bill, "", "", ""); }
            finally { LoadingOverlay.IsVisible = false; }
        }
        await _pdf.ShareBillAsync(_lastFilePath, _bill.BillNumber ?? "");
    }
}

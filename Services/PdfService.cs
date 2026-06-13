using GlassBilling.Models;

#if !ANDROID
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDFColor = QuestPDF.Infrastructure.Color;
using QuestPDFColors = QuestPDF.Helpers.Colors;
using QuestPDFContainer = QuestPDF.Infrastructure.IContainer;
#endif

namespace GlassBilling.Services;

public class PdfService
{
    // ── Public entry point ────────────────────────────────────────────────────

    public async Task<string> GenerateBillAsync(Bill bill,
        string companyName, string companyAddress, string companyPhone)
    {
#if ANDROID
        return await GenerateHtmlAsync(bill);
#else
        return await GeneratePdfAsync(bill);
#endif
    }

    public async Task ShareBillAsync(string filePath, string billNumber)
    {
        await Share.RequestAsync(new ShareFileRequest
        {
            Title = $"Bill {billNumber}",
            File  = new ShareFile(filePath)
        });
    }

    public async Task OpenBillAsync(string filePath)
    {
        await Launcher.OpenAsync(new OpenFileRequest
        {
            File = new ReadOnlyFile(filePath)
        });
    }

    // ── Android: HTML generation ──────────────────────────────────────────────

    private async Task<string> GenerateHtmlAsync(Bill bill)
    {
        string fileName = $"Bill_{bill.BillNumber?.Replace("-", "_")}_{DateTime.Now:yyyyMMdd}.html";
        string filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
        var html = await Task.Run(() => BuildHtml(bill));
        await File.WriteAllTextAsync(filePath, html);
        return filePath;
    }

    private static string BuildHtml(Bill bill)
    {
        string company  = Preferences.Get("company_name",    "Your Company Name");
        string address  = Preferences.Get("company_address", "");
        string phone    = Preferences.Get("company_phone",   "");
        string email    = Preferences.Get("company_email",   "");
        string bankName = Preferences.Get("bank_name",       "");
        string bankAcc  = Preferences.Get("bank_account",    "");
        string bankIfsc = Preferences.Get("bank_ifsc",       "");
        string bankBranch = Preferences.Get("bank_branch",   "");
        string upi      = Preferences.Get("upi_id",          "");
        string terms    = Preferences.Get("terms_conditions",
            "1. Goods once sold will not be taken back.\n2. Payment is due within 30 days.\n3. Disputes subject to local jurisdiction.");

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("""
            <!DOCTYPE html><html><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <title>Tax Invoice</title>
            <style>
              body { font-family: Arial, sans-serif; font-size: 12px; margin: 16px; color: #212121; }
              .header { display:flex; justify-content:space-between; align-items:flex-start; margin-bottom:10px; }
              .company-name { font-size:20px; font-weight:bold; color:#5B4FCF; }
              .sub { font-size:11px; color:#757575; margin-top:2px; }
              .invoice-label { font-size:16px; font-weight:bold; color:#5B4FCF; text-align:right; }
              hr { border:2px solid #5B4FCF; margin:8px 0 10px; }
              .bill-to { background:#F3F0FF; padding:10px; border-radius:6px; margin-bottom:10px; }
              .thickness-header { background:#5B4FCF; color:white; padding:6px 10px; border-radius:4px 4px 0 0;
                                  display:flex; justify-content:space-between; margin-top:10px; font-size:12px; }
              .thickness-meta { font-size:10px; color:#757575; padding:3px 8px; background:#FAFAFA; }
              table { width:100%; border-collapse:collapse; font-size:11px; }
              th { background:#EDE9FF; padding:5px 4px; text-align:center; color:#5B4FCF; }
              td { padding:5px 4px; border-bottom:1px solid #EEE; text-align:center; }
              td.left { text-align:left; }
              .subtotal-row { background:#F8F6FF; padding:5px 8px; display:flex; justify-content:space-between; font-size:12px; }
              .totals-section { margin-top:14px; border-top:2px solid #5B4FCF; padding-top:8px; }
              .totals-row { display:flex; justify-content:space-between; padding:3px 0; }
              .grand-total-row { display:flex; justify-content:space-between; font-size:17px; font-weight:bold; color:#5B4FCF; padding-top:6px; border-top:1px solid #5B4FCF; margin-top:4px; }
              .section { margin-top:14px; padding:10px; border:1px solid #E0E0E0; border-radius:6px; background:#FAFAFA; font-size:11px; }
              .section-title { font-weight:bold; color:#5B4FCF; margin-bottom:6px; font-size:12px; }
              .terms { white-space:pre-wrap; font-size:10px; color:#757575; }
              @media print { body { margin:8px; } }
            </style></head><body>
            """);

        // ── Header ─────────────────────────────────────────────────────────
        sb.AppendLine($"""
            <div class="header">
              <div>
                <div class="company-name">{Esc(company)}</div>
                {(string.IsNullOrWhiteSpace(address) ? "" : $"<div class='sub'>{Esc(address)}</div>")}
                {(string.IsNullOrWhiteSpace(phone)   ? "" : $"<div class='sub'>Ph: {Esc(phone)}</div>")}
                {(string.IsNullOrWhiteSpace(email)   ? "" : $"<div class='sub'>{Esc(email)}</div>")}
              </div>
              <div>
                <div class="invoice-label">TAX INVOICE</div>
                <div class="sub" style="text-align:right">Bill No: <b>{Esc(bill.BillNumber)}</b></div>
                <div class="sub" style="text-align:right">Date: {bill.FormattedDate}</div>
              </div>
            </div><hr>
            """);

        // ── Customer ────────────────────────────────────────────────────────
        sb.AppendLine($"""
            <div class="bill-to">
              <span style="font-size:11px;color:#5B4FCF"><b>Bill To:</b></span><br>
              <span style="font-size:15px;font-weight:bold">{Esc(bill.Customer?.Name)}</span><br>
              {(string.IsNullOrWhiteSpace(bill.Customer?.Location) ? "" : $"<span class='sub'>{Esc(bill.Customer.Location)}</span><br>")}
              {(string.IsNullOrWhiteSpace(bill.Customer?.Phone) ? "" : $"<span class='sub'>Ph: {Esc(bill.Customer.Phone)}</span>")}
            </div>
            """);

        // ── Items ───────────────────────────────────────────────────────────
        foreach (var item in bill.Items)
        {
            sb.AppendLine($"""
                <div class="thickness-header">
                  <span><b>{Esc(item.ThicknessName)}</b> &nbsp; {Esc(item.GlassDescription)}</span>
                  <span>Rate: ₹{item.PricePerSqFt:N2} / sq.ft</span>
                </div>
                <div class="thickness-meta">Shape: {Esc(item.Shape)} &nbsp;|&nbsp; Unit: {Esc(item.MeasurementUnit)} &nbsp;|&nbsp; Cutting Allowance: {item.CuttingAllowance}</div>
                <table>
                  <tr>
                    <th>#</th>
                    <th>Actual L</th><th>Actual W</th>
                    <th>Charge L</th><th>Charge W</th>
                    <th>Qty</th>
                    <th>Actual sq.ft</th>
                    <th>Charge sq.ft</th>
                  </tr>
                """);

            foreach (var row in item.Measurements)
            {
                sb.AppendLine($"""
                    <tr>
                      <td>{row.RowNumber}</td>
                      <td>{row.Length:G}</td><td>{row.Width:G}</td>
                      <td>{row.ChargeLength:G}</td><td>{row.ChargeWidth:G}</td>
                      <td>{row.Quantity}</td>
                      <td>{row.AreaInSqFt:N4}</td>
                      <td><b>{row.ChargeAreaSqFt:N4}</b></td>
                    </tr>
                    """);
            }

            sb.AppendLine($"""
                </table>
                <div class="subtotal-row">
                  <span>Total Charge Area: <b>{item.TotalChargeAreaSqFt:N4} sq.ft</b></span>
                  <span style="color:#5B4FCF"><b>Amount: ₹{item.TotalAmount:N2}</b></span>
                </div>
                """);
        }

        // ── Extra charges ───────────────────────────────────────────────────
        if (bill.ExtraCharges.Count > 0)
        {
            sb.AppendLine("""<div style="margin-top:10px"><b style="color:#5B4FCF">Extra Charges:</b><table><tr><th class="left">Description</th><th>Amount</th></tr>""");
            foreach (var ec in bill.ExtraCharges)
                sb.AppendLine($"<tr><td class='left'>{Esc(ec.Name)}</td><td>₹{ec.Amount:N2}</td></tr>");
            sb.AppendLine("</table></div>");
        }

        // ── Totals ──────────────────────────────────────────────────────────
        sb.AppendLine($"""
            <div class="totals-section">
              <div class="totals-row"><span>Sub Total</span><span>₹{bill.SubTotal:N2}</span></div>
              {(bill.ExtraChargesTotal > 0 ? $"<div class='totals-row'><span>Extra Charges</span><span>₹{bill.ExtraChargesTotal:N2}</span></div>" : "")}
              {(bill.TaxPercent > 0       ? $"<div class='totals-row'><span>Tax ({bill.TaxPercent:N0}%)</span><span>₹{bill.TaxAmount:N2}</span></div>" : "")}
              <div class="grand-total-row"><span>GRAND TOTAL</span><span>₹{bill.TotalAmount:N2}</span></div>
            </div>
            """);

        // ── Bank details ────────────────────────────────────────────────────
        bool hasBankInfo = !string.IsNullOrWhiteSpace(bankName) || !string.IsNullOrWhiteSpace(bankAcc);
        if (hasBankInfo)
        {
            sb.AppendLine("""<div class="section"><div class="section-title">Bank Details</div>""");
            if (!string.IsNullOrWhiteSpace(bankName))   sb.AppendLine($"<div>Bank: {Esc(bankName)}</div>");
            if (!string.IsNullOrWhiteSpace(bankAcc))    sb.AppendLine($"<div>A/C No: {Esc(bankAcc)}</div>");
            if (!string.IsNullOrWhiteSpace(bankIfsc))   sb.AppendLine($"<div>IFSC: {Esc(bankIfsc)}</div>");
            if (!string.IsNullOrWhiteSpace(bankBranch)) sb.AppendLine($"<div>Branch: {Esc(bankBranch)}</div>");
            if (!string.IsNullOrWhiteSpace(upi))        sb.AppendLine($"<div>UPI: {Esc(upi)}</div>");
            sb.AppendLine("</div>");
        }

        // ── Notes ───────────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(bill.Notes))
            sb.AppendLine($"""<div class="section"><div class="section-title">Notes</div>{Esc(bill.Notes)}</div>""");

        // ── Terms & Conditions ──────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(terms))
        {
            sb.AppendLine($"""
                <div class="section">
                  <div class="section-title">Terms &amp; Conditions</div>
                  <div class="terms">{Esc(terms)}</div>
                </div>
                """);
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static string Esc(string? s) =>
        System.Net.WebUtility.HtmlEncode(s ?? string.Empty);

    // ── Mac/Windows: QuestPDF generation ─────────────────────────────────────

#if !ANDROID
    private async Task<string> GeneratePdfAsync(Bill bill)
    {
        string company    = Preferences.Get("company_name",    "Your Company Name");
        string address    = Preferences.Get("company_address", "");
        string phone      = Preferences.Get("company_phone",   "");
        string email      = Preferences.Get("company_email",   "");
        string bankName   = Preferences.Get("bank_name",       "");
        string bankAcc    = Preferences.Get("bank_account",    "");
        string bankIfsc   = Preferences.Get("bank_ifsc",       "");
        string bankBranch = Preferences.Get("bank_branch",     "");
        string upi        = Preferences.Get("upi_id",          "");
        string terms      = Preferences.Get("terms_conditions",
            "1. Goods once sold will not be taken back.\n2. Payment is due within 30 days.\n3. Disputes subject to local jurisdiction.");

        string fileName = $"Bill_{bill.BillNumber?.Replace("-", "_")}_{DateTime.Now:yyyyMMdd}.pdf";
        string filePath = Path.Combine(FileSystem.CacheDirectory, fileName);

        await Task.Run(() =>
        {
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(25);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                    page.Header().Element(c => ComposeHeader(c, bill, company, address, phone, email));
                    page.Content().Element(c => ComposeContent(c, bill, bankName, bankAcc, bankIfsc, bankBranch, upi, terms));
                    page.Footer().Element(ComposeFooter);
                });
            })
            .GeneratePdf(filePath);
        });

        return filePath;
    }

    private static void ComposeHeader(QuestPDFContainer container, Bill bill,
        string company, string address, string phone, string email)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(company).FontSize(20).Bold().FontColor(QuestPDFColor.FromHex("#5B4FCF"));
                    if (!string.IsNullOrWhiteSpace(address))
                        c.Item().Text(address).FontSize(9).FontColor(QuestPDFColors.Grey.Medium);
                    if (!string.IsNullOrWhiteSpace(phone))
                        c.Item().Text($"Ph: {phone}").FontSize(9).FontColor(QuestPDFColors.Grey.Medium);
                    if (!string.IsNullOrWhiteSpace(email))
                        c.Item().Text(email).FontSize(9).FontColor(QuestPDFColors.Grey.Medium);
                });
                row.ConstantItem(160).AlignRight().Column(c =>
                {
                    c.Item().Text("TAX INVOICE").FontSize(16).Bold().FontColor(QuestPDFColor.FromHex("#5B4FCF"));
                    c.Item().Text($"Bill No: {bill.BillNumber}").FontSize(10).Bold();
                    c.Item().Text($"Date: {bill.FormattedDate}").FontSize(10);
                });
            });
            col.Item().PaddingTop(6).LineHorizontal(2).LineColor(QuestPDFColor.FromHex("#5B4FCF"));
        });
    }

    private static void ComposeContent(QuestPDFContainer container, Bill bill,
        string bankName, string bankAcc, string bankIfsc, string bankBranch, string upi, string terms)
    {
        container.Column(col =>
        {
            // Customer
            col.Item().PaddingTop(8).Background(QuestPDFColor.FromHex("#F3F0FF")).Padding(8).Column(c =>
            {
                c.Item().Text("Bill To:").Bold().FontSize(10).FontColor(QuestPDFColor.FromHex("#5B4FCF"));
                c.Item().Text(bill.Customer?.Name ?? "").Bold().FontSize(13);
                if (!string.IsNullOrWhiteSpace(bill.Customer?.Location))
                    c.Item().Text(bill.Customer.Location).FontSize(10);
                if (!string.IsNullOrWhiteSpace(bill.Customer?.Phone))
                    c.Item().Text($"Ph: {bill.Customer.Phone}").FontSize(9);
            });

            // Bill items
            foreach (var item in bill.Items)
            {
                col.Item().PaddingTop(8).Column(itemCol =>
                {
                    // Header
                    itemCol.Item().Background(QuestPDFColor.FromHex("#5B4FCF")).Padding(5).Row(r =>
                    {
                        r.RelativeItem().Text($"{item.ThicknessName}  {item.GlassDescription}").FontColor(QuestPDFColors.White).Bold().FontSize(10);
                        r.ConstantItem(130).AlignRight().Text($"₹{item.PricePerSqFt:N2}/sq.ft").FontColor(QuestPDFColors.White).FontSize(9);
                    });
                    // Meta
                    itemCol.Item().Background(QuestPDFColor.FromHex("#FAFAFA")).Padding(4)
                        .Text($"Shape: {item.Shape}  |  Unit: {item.MeasurementUnit}  |  Cutting Allowance: {item.CuttingAllowance}")
                        .FontSize(8).FontColor(QuestPDFColors.Grey.Medium);
                    // Column headers
                    itemCol.Item().Background(QuestPDFColor.FromHex("#EDE9FF")).Padding(4).Row(r =>
                    {
                        r.ConstantItem(22).AlignCenter().Text("#").Bold().FontSize(8).FontColor(QuestPDFColor.FromHex("#5B4FCF"));
                        r.RelativeItem().AlignCenter().Text("Actual L").Bold().FontSize(8).FontColor(QuestPDFColor.FromHex("#5B4FCF"));
                        r.RelativeItem().AlignCenter().Text("Actual W").Bold().FontSize(8).FontColor(QuestPDFColor.FromHex("#5B4FCF"));
                        r.RelativeItem().AlignCenter().Text("Charge L").Bold().FontSize(8).FontColor(QuestPDFColor.FromHex("#5B4FCF"));
                        r.RelativeItem().AlignCenter().Text("Charge W").Bold().FontSize(8).FontColor(QuestPDFColor.FromHex("#5B4FCF"));
                        r.ConstantItem(28).AlignCenter().Text("Qty").Bold().FontSize(8).FontColor(QuestPDFColor.FromHex("#5B4FCF"));
                        r.ConstantItem(55).AlignRight().Text("Act sq.ft").Bold().FontSize(8).FontColor(QuestPDFColor.FromHex("#5B4FCF"));
                        r.ConstantItem(60).AlignRight().Text("Chg sq.ft").Bold().FontSize(8).FontColor(QuestPDFColor.FromHex("#5B4FCF"));
                    });
                    // Rows
                    foreach (var mrow in item.Measurements)
                    {
                        var bg = mrow.RowNumber % 2 == 0
                            ? QuestPDFColor.FromHex("#FAFAFA")
                            : QuestPDFColors.White;
                        itemCol.Item().Background(bg).BorderBottom(0.5f).BorderColor(QuestPDFColors.Grey.Lighten3).Padding(4).Row(r =>
                        {
                            r.ConstantItem(22).AlignCenter().Text(mrow.RowNumber.ToString()).FontSize(8);
                            r.RelativeItem().AlignCenter().Text(mrow.Length.ToString("G")).FontSize(8);
                            r.RelativeItem().AlignCenter().Text(mrow.Width.ToString("G")).FontSize(8);
                            r.RelativeItem().AlignCenter().Text(mrow.ChargeLength.ToString("G")).FontSize(8);
                            r.RelativeItem().AlignCenter().Text(mrow.ChargeWidth.ToString("G")).FontSize(8);
                            r.ConstantItem(28).AlignCenter().Text(mrow.Quantity.ToString()).FontSize(8);
                            r.ConstantItem(55).AlignRight().Text(mrow.AreaInSqFt.ToString("N4")).FontSize(8);
                            r.ConstantItem(60).AlignRight().Text(mrow.ChargeAreaSqFt.ToString("N4")).Bold().FontSize(8);
                        });
                    }
                    // Subtotal
                    itemCol.Item().Background(QuestPDFColor.FromHex("#F8F6FF")).PaddingVertical(4).PaddingHorizontal(6).Row(r =>
                    {
                        r.RelativeItem().Text($"Total Charge Area: {item.TotalChargeAreaSqFt:N4} sq.ft").FontSize(9);
                        r.ConstantItem(150).AlignRight()
                            .Text($"Amount: ₹{item.TotalAmount:N2}").Bold().FontSize(10)
                            .FontColor(QuestPDFColor.FromHex("#5B4FCF"));
                    });
                });
            }

            // Extra charges
            if (bill.ExtraCharges.Count > 0)
            {
                col.Item().PaddingTop(10).Column(ec =>
                {
                    ec.Item().Text("Extra Charges").Bold().FontSize(10).FontColor(QuestPDFColor.FromHex("#5B4FCF"));
                    foreach (var e in bill.ExtraCharges)
                    {
                        ec.Item().Row(r =>
                        {
                            r.RelativeItem().Text(e.Name).FontSize(9);
                            r.ConstantItem(80).AlignRight().Text($"₹{e.Amount:N2}").FontSize(9);
                        });
                    }
                });
            }

            // Totals
            col.Item().PaddingTop(12).LineHorizontal(1.5f).LineColor(QuestPDFColor.FromHex("#5B4FCF"));
            col.Item().PaddingTop(6).Column(tot =>
            {
                tot.Item().Row(r =>
                {
                    r.RelativeItem().Text("Sub Total").FontSize(9);
                    r.ConstantItem(100).AlignRight().Text($"₹{bill.SubTotal:N2}").FontSize(9);
                });
                if (bill.ExtraChargesTotal > 0)
                    tot.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Extra Charges").FontSize(9);
                        r.ConstantItem(100).AlignRight().Text($"₹{bill.ExtraChargesTotal:N2}").FontSize(9);
                    });
                if (bill.TaxPercent > 0)
                    tot.Item().Row(r =>
                    {
                        r.RelativeItem().Text($"Tax ({bill.TaxPercent:N0}%)").FontSize(9);
                        r.ConstantItem(100).AlignRight().Text($"₹{bill.TaxAmount:N2}").FontSize(9);
                    });
                tot.Item().PaddingTop(4).LineHorizontal(1).LineColor(QuestPDFColor.FromHex("#5B4FCF"));
                tot.Item().PaddingTop(4).Row(r =>
                {
                    r.RelativeItem().Text("GRAND TOTAL").Bold().FontSize(15).FontColor(QuestPDFColor.FromHex("#5B4FCF"));
                    r.ConstantItem(130).AlignRight().Text($"₹{bill.TotalAmount:N2}").Bold().FontSize(15).FontColor(QuestPDFColor.FromHex("#5B4FCF"));
                });
            });

            // Bank details
            bool hasBankInfo = !string.IsNullOrWhiteSpace(bankName) || !string.IsNullOrWhiteSpace(bankAcc);
            if (hasBankInfo)
            {
                col.Item().PaddingTop(12).Column(b =>
                {
                    b.Item().Text("Bank Details").Bold().FontSize(10).FontColor(QuestPDFColor.FromHex("#5B4FCF"));
                    if (!string.IsNullOrWhiteSpace(bankName))   b.Item().Text($"Bank: {bankName}").FontSize(9);
                    if (!string.IsNullOrWhiteSpace(bankAcc))    b.Item().Text($"A/C No: {bankAcc}").FontSize(9);
                    if (!string.IsNullOrWhiteSpace(bankIfsc))   b.Item().Text($"IFSC: {bankIfsc}").FontSize(9);
                    if (!string.IsNullOrWhiteSpace(bankBranch)) b.Item().Text($"Branch: {bankBranch}").FontSize(9);
                    if (!string.IsNullOrWhiteSpace(upi))        b.Item().Text($"UPI: {upi}").FontSize(9);
                });
            }

            // Notes
            if (!string.IsNullOrWhiteSpace(bill.Notes))
            {
                col.Item().PaddingTop(10).Column(n =>
                {
                    n.Item().Text("Notes").Bold().FontSize(10).FontColor(QuestPDFColor.FromHex("#5B4FCF"));
                    n.Item().Text(bill.Notes).FontSize(9).FontColor(QuestPDFColors.Grey.Darken1);
                });
            }

            // Terms
            if (!string.IsNullOrWhiteSpace(terms))
            {
                col.Item().PaddingTop(10).Column(t =>
                {
                    t.Item().Text("Terms & Conditions").Bold().FontSize(10).FontColor(QuestPDFColor.FromHex("#5B4FCF"));
                    t.Item().Text(terms).FontSize(8).FontColor(QuestPDFColors.Grey.Medium);
                });
            }
        });
    }

    private static void ComposeFooter(QuestPDFContainer container)
    {
        container.Column(col =>
        {
            col.Item().LineHorizontal(0.5f).LineColor(QuestPDFColors.Grey.Lighten2);
            col.Item().PaddingTop(4).Row(r =>
            {
                r.RelativeItem().Text("Thank you for your business!").FontSize(8).FontColor(QuestPDFColors.Grey.Medium);
                r.ConstantItem(100).AlignRight()
                    .Text(ctx => ctx.CurrentPageNumber());
            });
        });
    }
#endif
}

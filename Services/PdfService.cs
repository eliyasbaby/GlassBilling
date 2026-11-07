using GlassBilling.Models;

#if WINDOWS
using System.Diagnostics;
#endif

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
    private readonly DatabaseService _db;
    private readonly AppLogService _log;

    public PdfService(DatabaseService db, AppLogService log)
    {
        _db = db;
        _log = log;
    }

    public async Task<string> GenerateBillAsync(Bill bill,
        string companyName, string companyAddress, string companyPhone)
    {
        await _log.InfoAsync($"PDF generation started. BillId={bill.Id}, BillNumber={bill.BillNumber ?? "(empty)"}");

        try
        {
#if ANDROID
            var filePath = await GenerateHtmlAsync(bill);
#else
            var filePath = await GeneratePdfAsync(bill);
#endif
            await LogFileAsync("PDF generation completed", filePath);
            return filePath;
        }
        catch (Exception ex)
        {
            await _log.ErrorAsync($"PDF generation failed. BillId={bill.Id}, BillNumber={bill.BillNumber ?? "(empty)"}", ex);
            throw;
        }
    }

    public async Task ShareBillAsync(string filePath, string billNumber)
    {
        await _log.InfoAsync($"Bill sharing started. BillNumber={billNumber}, File={filePath}");

        try
        {
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = $"Bill {billNumber}",
                File = new ShareFile(filePath)
            });
            await _log.InfoAsync($"Bill sharing completed. BillNumber={billNumber}");
        }
        catch (Exception ex)
        {
            await _log.ErrorAsync($"Bill sharing failed. BillNumber={billNumber}, File={filePath}", ex);
            throw;
        }
    }

    public async Task OpenBillAsync(string filePath)
    {
        await _log.InfoAsync($"Bill opening started. File={filePath}");

        try
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("The generated bill file was not found.", filePath);

            var opened = await Launcher.Default.OpenAsync(new OpenFileRequest
            {
                Title = "Open Bill",
                File = new ReadOnlyFile(filePath)
            });

            if (opened)
            {
                await _log.InfoAsync($"Bill opened with the default file handler. File={filePath}");
                return;
            }

#if WINDOWS
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });

            if (process is null)
                throw new InvalidOperationException("No application is registered to open PDF files.");

            await _log.InfoAsync($"Bill opened with the Windows shell fallback. File={filePath}");
#else
            throw new InvalidOperationException("No application is registered to open this file.");
#endif
        }
        catch (Exception ex)
        {
            await _log.ErrorAsync($"Bill opening failed. File={filePath}", ex);
            throw;
        }
    }

    private async Task LogFileAsync(string message, string filePath)
    {
        var length = File.Exists(filePath) ? new FileInfo(filePath).Length : 0;
        await _log.InfoAsync($"{message}. File={filePath}, Exists={File.Exists(filePath)}, Bytes={length}");
    }

    private async Task<string> GenerateHtmlAsync(Bill bill)
    {
        string fileName = BuildBillFileName(bill, ".html");
        string directory = GetWritableDirectory();
        Directory.CreateDirectory(directory);
        string filePath = Path.Combine(directory, fileName);
        var s = await _db.GetSettingsAsync();
        var logoBytes = await LoadImageBytesAsync("glasslogo.png");
        var upiBytes = await LoadImageBytesAsync("upi.jpeg");
        await File.WriteAllTextAsync(filePath, BuildHtml(bill, s, logoBytes, upiBytes));
        return filePath;
    }

    private static string BuildHtml(Bill bill, GlassBilling.Models.Settings s, byte[]? logoBytes, byte[]? upiBytes)
    {
        string company = s.CompanyName ?? string.Empty;
        string address = s.CompanyAddress ?? string.Empty;
        string phone = s.CompanyPhone ?? string.Empty;
        string email = s.CompanyEmail ?? string.Empty;
        string gstin = s.CompanyGstin ?? string.Empty;
        string officeNo = phone;
        string bankName = s.BankName ?? string.Empty;
        string bankAcc = s.BankAccount ?? string.Empty;
        string bankIfsc = s.BankIfsc ?? string.Empty;
        string bankBranch = s.BankBranch ?? string.Empty;
        string upi = s.UpiId ?? string.Empty;
        string terms = string.IsNullOrWhiteSpace(s.TermsConditions)
            ? "1. Please check the size, specification, and rate before confirmation.\n" +
              "2. 60% advance along with the purchase order (payment mode: bank or cash).\n" +
              "3. Balance to be paid before delivery.\n" +
              "4. We are not responsible for any damage or breakage after delivery.\n" +
              "5. Site unloading should be done by the customer."
            : s.TermsConditions;
        string logoMarkup = logoBytes is null
            ? string.Empty
            : "<img class=\"logo\" src=\"" + ImageDataUri(logoBytes, "image/png") + "\" alt=\"Company logo\">";
        string upiMarkup = upiBytes is null
            ? string.Empty
            : "<img class=\"qr\" src=\"" + ImageDataUri(upiBytes, "image/jpeg") + "\" alt=\"UPI QR code\">";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("""
            <!doctype html>
            <html>
            <head>
              <meta charset="utf-8">
              <title>Proforma Invoice</title>
              <style>
                body { font-family: Arial, sans-serif; color: #000; margin: 16px; }
                .page { width: 790px; margin: 0 auto; background: #fff; }
                .top { display: grid; grid-template-columns: 170px 1fr; align-items: center; }
                .logo { width: 150px; }
                h1 { color: #17436E; text-align: center; margin: 0; font-size: 33px; }
                .center { text-align: center; }
                .rule { border-top: 1px solid #999; margin: 2px 0; }
                .two { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; font-size: 16px; }
                .title { text-align: center; font-size: 24px; font-weight: 700; text-decoration: underline; }
                table { width: 100%; border-collapse: collapse; font-size: 14px; }
                th, td { border: 1px solid #8f8f8f; padding: 3px 5px; text-align: center; }
                th { background: #eee; }
                .item-title { display: flex; justify-content: space-between; font-size: 16px; font-weight: 700; }
                .bottom { display: grid; grid-template-columns: 280px 210px 1fr; gap: 10px; margin-top: 10px; }
                .small { font-size: 12px; }
                .section-title { font-size: 21px; font-weight: 700; margin-bottom: 12px; }
                .qr { width: 180px; }
              </style>
            </head>
            <body><div class="page">
            """);

        sb.AppendLine($"""
            <div class="top">
                            {logoMarkup}
              <div>
                <h1>{Esc(company)}</h1>
                <div class="center">{Esc(address).Replace("\n", "<br>")}</div>
                <div class="center">PH: {Esc(phone)}</div>
              </div>
            </div>
            <div class="rule"></div>
            <div class="two">
              <div>
                <b>Customer Details</b><br>
                Name: {Esc(bill.Customer?.Name)}<br>
                Location: {Esc(bill.Customer?.Location)}<br>
                Phone: {Esc(bill.Customer?.Phone)}<br>
                Email:<br>
                GST No:
              </div>
              <div>
                Date : {bill.BillDate:dd-MM-yyyy}<br>
                PI No: {Esc(bill.BillNumber)}<br>
                Office: {Esc(officeNo)}<br>
                E-mail: {Esc(email)}<br>
                GSTIN: {Esc(gstin)}<br>
                Salesman:
              </div>
            </div>
            <div class="rule"></div>
            <div class="title">PROFORMA INVOICE</div>
            <div class="rule"></div>
            """);

        foreach (var item in bill.Items)
        {
            sb.AppendLine($"""
                <div class="item-title"><span>{Esc(item.ThicknessName)} {Esc(item.GlassDescription)}</span><span>Unit:({Esc(item.MeasurementUnit)})</span></div>
                <table>
                  <tr>
                    <th rowspan="2">Sl No.</th><th colspan="2">Actual Size</th><th colspan="2">Charge Size</th>
                    <th rowspan="2">Qty</th><th rowspan="2">Chargeable Area</th><th rowspan="2">Rate<br>(&#8377;/sqft)</th>
                    <th rowspan="2">Shape</th><th rowspan="2">Amount<br>(&#8377;)</th>
                  </tr>
                  <tr><th>Height</th><th>Width</th><th>Height</th><th>Width</th></tr>
                """);

            int sr = 1;
            foreach (var row in item.Measurements)
            {
                sb.AppendLine($"""
                    <tr>
                      <td>{sr}</td><td>{row.Length:N2}</td><td>{row.Width:N2}</td>
                      <td>{row.ChargeLength:N2}</td><td>{row.ChargeWidth:N2}</td>
                      <td>{row.Quantity}</td><td>{row.ChargeAreaSqFt:N2}</td>
                      <td>{item.PricePerSqFt:N3}</td><td>{Esc(row.Shape)}</td>
                      <td>{row.ChargeAreaSqFt * item.PricePerSqFt:N2}</td>
                    </tr>
                    """);
                sr++;
            }

            sb.AppendLine($"""
                <tr><td colspan="5"><b>Totals:</b></td><td>{item.Measurements.Sum(row => row.Quantity)}</td><td>{item.TotalChargeAreaSqFt:N2}</td><td></td><td></td><td>{item.TotalAmount:N2}</td></tr>
                </table>
                """);
        }

        sb.AppendLine($"""
            <div class="bottom">
              <div class="small">
                <b>Account Number:</b> {Esc(bankAcc)}<br>
                <b>IFSC Code:</b> {Esc(bankIfsc)}<br>
                <b>Bank:</b> {Esc(bankName)}<br>
                <b>Branch:</b> {Esc(bankBranch)}<br>
                <b>UPI ID:</b> {Esc(upi)}<br><br>
                <b>Terms and Conditions</b><br>{Esc(terms).Replace("\n", "<br>")}
              </div>
              <div class="center">{upiMarkup}</div>
              <div>
                <div class="section-title">Extra Charges</div>
                <table>
            """);

        if (bill.ExtraCharges.Count == 0)
            sb.AppendLine("<tr><td>No extra charges</td></tr>");
        foreach (var charge in bill.ExtraCharges)
            sb.AppendLine($"<tr><td><b>{Esc(charge.Name)}</b></td><td>{charge.Amount:N1}</td></tr>");

        sb.AppendLine($"""
                </table>
                <div class="section-title" style="margin-top:18px">Total Calculation</div>
                <table>
                  <tr><td><b>Total (&#8377;)</b></td><td>{bill.SubTotal:N2}</td></tr>
                  <tr><td><b>Tax ({bill.TaxPercent:N0}%)</b></td><td>{bill.TaxAmount:N2}</td></tr>
                  <tr><td><b>Grand Total (&#8377;)</b></td><td><b>{bill.TotalAmount:N0}</b></td></tr>
                </table>
              </div>
            </div>
            <div class="rule"></div>
            </div></body></html>
            """);

        return sb.ToString();
    }

    private static string ImageDataUri(byte[] imageBytes, string mediaType) =>
        $"data:{mediaType};base64,{Convert.ToBase64String(imageBytes)}";

    private static string Esc(string? s) =>
        System.Net.WebUtility.HtmlEncode(s ?? string.Empty);

#if !ANDROID
    private async Task<string> GeneratePdfAsync(Bill bill)
    {
        var s = await _db.GetSettingsAsync();
        string company = s.CompanyName ?? string.Empty;
        string address = s.CompanyAddress ?? string.Empty;
        string phone = s.CompanyPhone ?? string.Empty;
        string email = s.CompanyEmail ?? string.Empty;
        string gstin = s.CompanyGstin ?? string.Empty;
        string officeNo = phone;
        string bankName = s.BankName ?? string.Empty;
        string bankAcc = s.BankAccount ?? string.Empty;
        string bankIfsc = s.BankIfsc ?? string.Empty;
        string bankBranch = s.BankBranch ?? string.Empty;
        string upi = s.UpiId ?? string.Empty;
        string terms = string.IsNullOrWhiteSpace(s.TermsConditions)
            ? "1. Please check the size, specification, and rate before confirmation.\n" +
              "2. 60% advance along with the purchase order (payment mode: bank or cash).\n" +
              "3. Balance to be paid before delivery.\n" +
              "4. We are not responsible for any damage or breakage after delivery.\n" +
              "5. Site unloading should be done by the customer."
            : s.TermsConditions;

        var logoBytes = await LoadImageBytesAsync("glasslogo.png");
        var upiBytes = await LoadImageBytesAsync("upi.jpeg");

        string fileName = BuildBillFileName(bill, ".pdf");
        string directory = GetWritableDirectory();
        Directory.CreateDirectory(directory);
        string filePath = Path.Combine(directory, fileName);

        await Task.Run(() =>
        {
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(22);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));
                    page.Content().Column(col =>
                    {
                        col.Item().Element(c => ComposeInvoiceHeader(c, company, address, phone, logoBytes));
                        col.Item().LineHorizontal(0.45f).LineColor(QuestPDFColors.Grey.Medium);
                        col.Item().Element(c => ComposeCustomerBlock(c, bill, officeNo, email, gstin));
                        col.Item().LineHorizontal(0.45f).LineColor(QuestPDFColors.Grey.Medium);
                        col.Item().AlignCenter().Text("PROFORMA INVOICE").FontSize(15).SemiBold().Underline();
                        col.Item().LineHorizontal(0.45f).LineColor(QuestPDFColors.Grey.Medium);
                        col.Item().Element(c => ComposeItems(c, bill));
                        col.Item().PaddingTop(8).Element(c => ComposeBottom(c, bill, bankName, bankAcc, bankIfsc, bankBranch, upi, terms, upiBytes));

                        if (!string.IsNullOrWhiteSpace(bill.Notes))
                            col.Item().PaddingTop(6).Border(0.5f).Padding(5).Column(n =>
                            {
                                n.Item().Text("Notes:").Bold();
                                n.Item().Text(bill.Notes);
                            });

                        col.Item().PaddingTop(6).LineHorizontal(0.45f).LineColor(QuestPDFColors.Grey.Medium);
                    });
                });
            })
            .GeneratePdf(filePath);
        });

        return filePath;
    }

    private static string BuildBillFileName(Bill bill, string extension)
    {
        var billNumber = string.IsNullOrWhiteSpace(bill.BillNumber) ? "Unnumbered" : bill.BillNumber;
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var safeBillNumber = new string(billNumber
            .Select(character => invalidCharacters.Contains(character) ? '_' : character)
            .ToArray());

        return $"Bill_{safeBillNumber}_{DateTime.Now:yyyyMMdd_HHmmssfff}_{Guid.NewGuid():N}{extension}";
    }

    private static string GetWritableDirectory()
    {
        string path = Path.Combine(FileSystem.AppDataDirectory, "Bills");
        return path;
    }

    private static async Task<byte[]?> LoadImageBytesAsync(string fileName)
    {
        try
        {
            await using var packageStream = await FileSystem.OpenAppPackageFileAsync(fileName);
            using var ms = new MemoryStream();
            await packageStream.CopyToAsync(ms);
            return ms.ToArray();
        }
        catch (FileNotFoundException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (NotSupportedException)
        {
        }

        var diskCandidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, fileName),
            Path.Combine(AppContext.BaseDirectory, "Images", fileName),
            Path.Combine(Environment.CurrentDirectory, fileName)
        };

        foreach (var path in diskCandidates)
        {
            if (File.Exists(path))
                return await File.ReadAllBytesAsync(path);
        }

        return null;
    }

    private static void ComposeInvoiceHeader(QuestPDFContainer container, string company, string address, string phone, byte[]? logoBytes)
    {
        container.Row(row =>
        {
            row.ConstantItem(150).Height(70).AlignRight().Element(c =>
            {
                if (logoBytes is not null)
                    c.Image(logoBytes).FitArea();
            });

            row.RelativeItem().PaddingLeft(18).Column(c =>
            {
                c.Item().AlignCenter().Text(company).FontSize(22).Bold().FontColor(QuestPDFColor.FromHex("#17436E"));
                if (!string.IsNullOrWhiteSpace(address))
                    c.Item().AlignCenter().Text(address).FontSize(11);
                if (!string.IsNullOrWhiteSpace(phone))
                    c.Item().AlignCenter().Text($"PH: {phone}").FontSize(11);
            });
        });
    }

    private static void ComposeCustomerBlock(QuestPDFContainer container, Bill bill, string officeNo, string email, string gstin)
    {
        container.PaddingVertical(3).Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().Text("Customer Details").Bold().FontSize(11);
                c.Item().Text($"Name: {bill.Customer?.Name ?? ""}").FontSize(11);
                c.Item().Text($"Location: {bill.Customer?.Location ?? ""}").FontSize(11);
                c.Item().Text($"Phone: {bill.Customer?.Phone ?? ""}").FontSize(11);
                c.Item().Text("Email:").FontSize(11);
                c.Item().Text("GST No:").FontSize(11);
            });

            row.RelativeItem().Column(c =>
            {
                c.Item().Text($"Date : {bill.BillDate:dd-MM-yyyy}").FontSize(11);
                c.Item().Text($"PI No: {bill.BillNumber}").FontSize(11);
                c.Item().Text($"Office: {officeNo}").FontSize(11);
                c.Item().Text($"E-mail: {email}").FontSize(11);
                c.Item().Text($"GSTIN: {gstin}").FontSize(11);
                c.Item().Text("Salesman:").FontSize(11);
            });
        });
    }

    private static void ComposeItems(QuestPDFContainer container, Bill bill)
    {
        container.Column(col =>
        {
            foreach (var item in bill.Items)
            {
                col.Item().Row(r =>
                {
                    r.RelativeItem().Text($"{item.ThicknessName} {item.GlassDescription}").Bold().FontSize(11);
                    r.ConstantItem(95).AlignRight().Text($"Unit:({item.MeasurementUnit})").FontSize(11);
                });

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(42);
                        columns.ConstantColumn(52);
                        columns.ConstantColumn(52);
                        columns.ConstantColumn(54);
                        columns.ConstantColumn(54);
                        columns.ConstantColumn(34);
                        columns.ConstantColumn(63);
                        columns.ConstantColumn(58);
                        columns.ConstantColumn(58);
                        columns.RelativeColumn();
                    });

                    AddHeader(table);

                    int sr = 1;
                    foreach (var row in item.Measurements)
                    {
                        AddBodyCell(table, sr.ToString());
                        AddBodyCell(table, row.Length.ToString("N2"));
                        AddBodyCell(table, row.Width.ToString("N2"));
                        AddBodyCell(table, row.ChargeLength.ToString("N2"));
                        AddBodyCell(table, row.ChargeWidth.ToString("N2"));
                        AddBodyCell(table, row.Quantity.ToString());
                        AddBodyCell(table, row.ChargeAreaSqFt.ToString("N2"));
                        AddBodyCell(table, item.PricePerSqFt.ToString("N3"));
                        AddBodyCell(table, row.Shape);
                        AddBodyCell(table, (row.ChargeAreaSqFt * item.PricePerSqFt).ToString("N2"), true);
                        sr++;
                    }

                    AddBodyCell(table, "Totals:", true);
                    AddBodyCell(table, "");
                    AddBodyCell(table, "");
                    AddBodyCell(table, "");
                    AddBodyCell(table, "");
                    AddBodyCell(table, item.Measurements.Sum(row => row.Quantity).ToString(), true);
                    AddBodyCell(table, item.TotalChargeAreaSqFt.ToString("N2"));
                    AddBodyCell(table, "");
                    AddBodyCell(table, "");
                    AddBodyCell(table, item.TotalAmount.ToString("N2"), true);
                });
            }
        });
    }

    private static void AddHeader(TableDescriptor table)
    {
        AddHeaderCell(table, "Sl No.", rowSpan: 2);
        AddHeaderCell(table, "Actual Size", columnSpan: 2);
        AddHeaderCell(table, "Charge Size", columnSpan: 2);
        AddHeaderCell(table, "Qty", rowSpan: 2);
        AddHeaderCell(table, "Chargeable Area", rowSpan: 2);
        AddHeaderCell(table, "Rate\n(\u20B9/sqft)", rowSpan: 2);
        AddHeaderCell(table, "Shape", rowSpan: 2);
        AddHeaderCell(table, "Amount\n(\u20B9)", rowSpan: 2);
        AddHeaderCell(table, "Height");
        AddHeaderCell(table, "Width");
        AddHeaderCell(table, "Height");
        AddHeaderCell(table, "Width");
    }

    private static void AddHeaderCell(TableDescriptor table, string text, uint rowSpan = 1, uint columnSpan = 1)
    {
        var cell = table.Cell();
        if (rowSpan > 1) cell = cell.RowSpan(rowSpan);
        if (columnSpan > 1) cell = cell.ColumnSpan(columnSpan);
        cell.Element(HeaderCellStyle).AlignCenter().AlignMiddle().Text(text).Bold().FontSize(8);
    }

    private static void AddBodyCell(TableDescriptor table, string text, bool bold = false)
    {
        var descriptor = table.Cell().Element(BodyCellStyle).AlignCenter().AlignMiddle().Text(text).FontSize(8);
        if (bold)
            descriptor.Bold();
    }

    private static QuestPDFContainer HeaderCellStyle(QuestPDFContainer c) =>
        c.Border(0.5f).BorderColor(QuestPDFColors.Grey.Medium).Background(QuestPDFColors.Grey.Lighten3).Padding(3);

    private static QuestPDFContainer BodyCellStyle(QuestPDFContainer c) =>
        c.Border(0.5f).BorderColor(QuestPDFColors.Grey.Medium).Padding(3);

    private static void ComposeBottom(QuestPDFContainer container, Bill bill, string bankName, string bankAcc,
        string bankIfsc, string bankBranch, string upi, string terms, byte[]? upiBytes)
    {
        container.Row(row =>
        {
            row.ConstantItem(210).Column(left =>
            {
                BankLine(left, "Account Number:", bankAcc);
                BankLine(left, "IFSC Code:", bankIfsc);
                BankLine(left, "Bank:", bankName);
                BankLine(left, "Branch:", bankBranch);
                BankLine(left, "UPI ID:", upi);

                left.Item().PaddingTop(12).Text("Terms and Conditions").Bold().FontSize(8);
                left.Item().Text(terms).FontSize(7);
            });

            row.ConstantItem(160).AlignCenter().Height(130).Element(c =>
            {
                if (upiBytes is not null)
                    c.Image(upiBytes).FitArea();
            });

            row.RelativeItem().PaddingLeft(18).Column(right =>
            {
                right.Item().Text("Extra Charges").Bold().FontSize(13);
                right.Item().PaddingTop(12).Element(c => ComposeExtraCharges(c, bill));
                right.Item().PaddingTop(12).Text("Total Calculation").Bold().FontSize(13);
                right.Item().PaddingTop(8).Element(c => ComposeTotals(c, bill));
            });
        });
    }

    private static void BankLine(ColumnDescriptor column, string label, string value)
    {
        column.Item().Row(r =>
        {
            r.ConstantItem(68).Text(label).Bold().FontSize(8);
            r.RelativeItem().Text(value).FontSize(8);
        });
    }

    private static void ComposeExtraCharges(QuestPDFContainer container, Bill bill)
    {
        if (bill.ExtraCharges.Count == 0)
        {
            container.Text("No extra charges").FontSize(8);
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn();
                c.ConstantColumn(72);
            });

            foreach (var charge in bill.ExtraCharges)
            {
                AddBodyCell(table, charge.Name, true);
                AddBodyCell(table, charge.Amount.ToString("N1"));
            }
        });
    }

    private static void ComposeTotals(QuestPDFContainer container, Bill bill)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(c =>
            {
                c.RelativeColumn();
                c.ConstantColumn(72);
            });

            AddBodyCell(table, "Total (\u20B9)", true);
            AddBodyCell(table, bill.SubTotal.ToString("N2"));
            AddBodyCell(table, $"Tax ({bill.TaxPercent:N0}%)", true);
            AddBodyCell(table, bill.TaxAmount.ToString("N2"));
            AddBodyCell(table, "Discount (\u20B9)", true);
            AddBodyCell(table, bill.DiscountAmount.ToString("N2"));
            AddBodyCell(table, "Grand Total (\u20B9)", true);
            AddBodyCell(table, bill.TotalAmount.ToString("N0"), true);
        });
    }
#endif
}

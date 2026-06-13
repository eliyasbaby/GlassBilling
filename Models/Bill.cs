using SQLite;

namespace GlassBilling.Models;

[Table("Bills")]
public class Bill
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull]
    public string BillNumber { get; set; } = string.Empty;

    public int CustomerId { get; set; }

    public DateTime BillDate { get; set; } = DateTime.Now;

    public double SubTotal { get; set; }          // sum of all BillItems before extra/tax

    public double ExtraChargesTotal { get; set; } // sum of ExtraCharges

    public double TaxPercent { get; set; } = 0;  // e.g. 18 for 18%

    public double TaxAmount { get; set; }

    public double TotalAmount { get; set; }       // SubTotal + ExtraChargesTotal + TaxAmount

    public string Notes { get; set; } = string.Empty;

    // Navigation (not stored)
    [Ignore] public Customer? Customer { get; set; }
    [Ignore] public List<BillItem> Items { get; set; } = new();
    [Ignore] public List<ExtraCharge> ExtraCharges { get; set; } = new();

    [Ignore] public string FormattedDate  => BillDate.ToString("dd/MM/yyyy");
    [Ignore] public string FormattedTotal => $"₹{TotalAmount:N2}";

    /// <summary>Recalculate SubTotal, TaxAmount, TotalAmount from current items/extra charges</summary>
    public void Recalculate()
    {
        SubTotal          = Items.Sum(i => i.TotalAmount);
        ExtraChargesTotal = ExtraCharges.Sum(e => e.Amount);
        TaxAmount         = (SubTotal + ExtraChargesTotal) * TaxPercent / 100.0;
        TotalAmount       = SubTotal + ExtraChargesTotal + TaxAmount;
    }
}

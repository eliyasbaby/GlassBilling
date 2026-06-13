using SQLite;

namespace GlassBilling.Models;

[Table("BillItems")]
public class BillItem
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int BillId { get; set; }

    public int ThicknessTypeId { get; set; }

    public string ThicknessName { get; set; } = string.Empty;  // e.g. "12mm"

    public string GlassDescription { get; set; } = "Toughened Plain Glass";

    public string MeasurementUnit { get; set; } = "mm";

    /// <summary>Added to each dimension for chargeable size (e.g. 2 inches cutting allowance)</summary>
    public double CuttingAllowance { get; set; } = 0;

    public double PricePerSqFt { get; set; }

    public string Shape { get; set; } = "Block";

    public double TotalChargeAreaSqFt { get; set; }   // chargeable area (with cutting allowance)

    public double TotalAmount { get; set; }

    // Navigation
    [Ignore] public List<MeasurementRow> Measurements { get; set; } = new();

    [Ignore] public string FormattedAmount => $"₹{TotalAmount:N2}";

    [Ignore] public string FullName => $"{ThicknessName} {GlassDescription}";
}

using SQLite;

namespace GlassBilling.Models;

[Table("MeasurementRows")]
public class MeasurementRow
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int BillItemId { get; set; }

    public double Length { get; set; }       // Actual height/length

    public double Width { get; set; }        // Actual width

    public double ChargeLength { get; set; } // Length + CuttingAllowance

    public double ChargeWidth { get; set; }  // Width  + CuttingAllowance

    public string Unit { get; set; } = "mm";

    public int Quantity { get; set; } = 1;

    public double AreaInSqFt { get; set; }       // actual area

    public double ChargeAreaSqFt { get; set; }   // chargeable area (charge size × qty)

    public int RowNumber { get; set; }

    // ── Unit conversion ───────────────────────────────────────────────────────

    public static double ToFeet(double value, string unit) => unit switch
    {
        "mm"   => value / 304.8,
        "cm"   => value / 30.48,
        "inch" => value / 12.0,
        "feet" => value,
        _      => value / 304.8
    };

    public static double CalcAreaSqFt(double length, double width, string unit, int qty = 1)
        => ToFeet(length, unit) * ToFeet(width, unit) * qty;

    // Calculate the chargeable area in sq.ft. by adding 2 inches to both
    // length and width to account for wastage before converting to feet.
    public static double CalcChargeableAreaSqFt(double length, double width, string unit, int qty = 1)
    {
        const double extraInInches = 2.0; // 2 inch wasteage per dimension
        // convert extra inches to feet
        double extraFeet = extraInInches / 12.0;

        return (ToFeet(length, unit) + extraFeet) * (ToFeet(width, unit) + extraFeet) * qty;
    }

    [Ignore] public string DisplayArea       => $"{AreaInSqFt:N4}";
    [Ignore] public string DisplayChargeArea => $"{ChargeAreaSqFt:N4}";
}

using SQLite;

namespace GlassBilling.Models;

[Table("ExtraCharges")]
public class ExtraCharge
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public int BillId { get; set; }

    public string Name { get; set; } = string.Empty;   // e.g. "Transportation"

    public double Amount { get; set; }

    [Ignore]
    public string FormattedAmount => $"₹{Amount:N2}";
}

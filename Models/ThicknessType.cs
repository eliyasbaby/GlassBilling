using SQLite;

namespace GlassBilling.Models;

[Table("ThicknessTypes")]
public class ThicknessType
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull]
    public string Name { get; set; } = string.Empty;  // e.g., "5mm", "6mm", "8mm"

    public double ThicknessValue { get; set; }         // e.g., 5.0

    public string ThicknessUnit { get; set; } = "mm";  // always mm for glass

    public double PricePerSqFt { get; set; }           // price per square foot

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    // Default glass thickness types
    public static List<ThicknessType> Defaults => new()
    {
        new() { Name = "3mm",  ThicknessValue = 3,  PricePerSqFt = 0, IsActive = true, SortOrder = 1 },
        new() { Name = "4mm",  ThicknessValue = 4,  PricePerSqFt = 0, IsActive = true, SortOrder = 2 },
        new() { Name = "5mm",  ThicknessValue = 5,  PricePerSqFt = 0, IsActive = true, SortOrder = 3 },
        new() { Name = "6mm",  ThicknessValue = 6,  PricePerSqFt = 0, IsActive = true, SortOrder = 4 },
        new() { Name = "8mm",  ThicknessValue = 8,  PricePerSqFt = 0, IsActive = true, SortOrder = 5 },
        new() { Name = "10mm", ThicknessValue = 10, PricePerSqFt = 0, IsActive = true, SortOrder = 6 },
        new() { Name = "12mm", ThicknessValue = 12, PricePerSqFt = 0, IsActive = true, SortOrder = 7 },
        new() { Name = "15mm", ThicknessValue = 15, PricePerSqFt = 0, IsActive = true, SortOrder = 8 },
        new() { Name = "19mm", ThicknessValue = 19, PricePerSqFt = 0, IsActive = true, SortOrder = 9 },
    };
}

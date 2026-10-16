using SQLite;

namespace GlassBilling.Models;

[Table("GlassTypes")]
public class GlassType
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull]
    public string Name { get; set; } = string.Empty;

    public double WastageInches { get; set; } = 2;

    public int SortOrder { get; set; }

    public static List<GlassType> Defaults => new()
    {
        new() { Name = "Clear glass", WastageInches = 0, SortOrder = 1 },
        new() { Name = "Cooling glass", WastageInches = 2, SortOrder = 2 },
        new() { Name = "Brown glass", WastageInches = 4, SortOrder = 3 },
        new() { Name = "Fluted glass", WastageInches = 6, SortOrder = 4 }
    };
}
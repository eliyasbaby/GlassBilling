using SQLite;

namespace GlassBilling.Models;

[Table("GlassShapes")]
public class GlassShape
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull]
    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public static List<GlassShape> Defaults => new()
    {
        new() { Name = "Block", SortOrder = 1 },
        new() { Name = "L Shape", SortOrder = 2 },
        new() { Name = "Round", SortOrder = 3 },
        new() { Name = "Oval", SortOrder = 4 },
        new() { Name = "Triangle", SortOrder = 5 }
    };
}
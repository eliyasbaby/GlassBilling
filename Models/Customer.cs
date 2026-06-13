using SQLite;

namespace GlassBilling.Models;

[Table("Customers")]
public class Customer
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [NotNull]
    public string Name { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Display name for dropdowns: "Name | Location"
    [Ignore]
    public string DisplayName => string.IsNullOrWhiteSpace(Location)
        ? Name
        : $"{Name} | {Location}";
}

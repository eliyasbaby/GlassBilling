using SQLite;

namespace GlassBilling.Models;

[Table("AppSettings")]
public class Settings
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    // Company
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyAddress { get; set; } = string.Empty;
    public string CompanyPhone { get; set; } = string.Empty;
    public string CompanyEmail { get; set; } = string.Empty;
    public string CompanyGstin { get; set; } = string.Empty;
    public double DefaultTax { get; set; } = 18;

    // Bank
    public string BankName { get; set; } = string.Empty;
    public string BankAccount { get; set; } = string.Empty;
    public string BankIfsc { get; set; } = string.Empty;
    public string BankBranch { get; set; } = string.Empty;
    public string UpiId { get; set; } = string.Empty;

    // Terms
    public string TermsConditions { get; set; } = string.Empty;

    // Salesman / logged-in user to appear on bills
    public string SalesmanName { get; set; } = string.Empty;
}

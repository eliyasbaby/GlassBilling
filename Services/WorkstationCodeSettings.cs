namespace GlassBilling.Services;

internal static class WorkstationCodeSettings
{
    private const string PreferenceKey = "workstation_code";

    public static string GetCode() => Normalize(Preferences.Get(PreferenceKey, string.Empty));

    public static void SetCode(string code) => Preferences.Set(PreferenceKey, Normalize(code));

    public static string Normalize(string? code) => (code ?? string.Empty).Trim().ToUpperInvariant();

    public static bool IsValid(string? code) =>
        code is { Length: 1 } && char.IsAsciiLetter(code[0]);
}
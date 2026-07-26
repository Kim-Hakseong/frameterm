namespace Ft.Core.Parsing;

/// <summary>
/// A parsed field. When the frame is too short for the field's range the
/// value is unavailable and displays as "N/A" (only that field degrades).
/// </summary>
public sealed record FieldValue(string Name, bool IsAvailable, double Numeric, string Display)
{
    public static FieldValue NotAvailable(string name) => new(name, false, double.NaN, "N/A");
}

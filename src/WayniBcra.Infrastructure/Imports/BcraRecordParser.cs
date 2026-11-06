using System.Globalization;
using WayniBcra.Application.Imports;

namespace WayniBcra.Infrastructure.Imports;

/// <summary>
/// Parses a single line of the BCRA fixed-width "deudores.txt" file into a <see cref="BcraRecord"/>.
/// Pure, deterministic and I/O-free so it can be unit tested in isolation.
/// </summary>
/// <remarks>
/// Layout (0-based, half-open ranges), verified against the real file (171 chars/line):
/// field 1 EntityCode [0:5], field 3 IdentificationType [11:13], field 4 IdentificationNumber [13:24],
/// field 6 CreditStatus [27:29], field 7 FinancingAmount [29:41].
/// Only the fields used by this solution are extracted.
/// </remarks>
public static class BcraRecordParser
{
    /// <summary>Expected fixed line width in characters.</summary>
    public const int LineWidth = 171;

    // Field offsets [start, end).
    private static readonly (int Start, int End) EntityCodeField = (0, 5);
    private static readonly (int Start, int End) IdentificationTypeField = (11, 13);
    private static readonly (int Start, int End) IdentificationNumberField = (13, 24);
    private static readonly (int Start, int End) CreditStatusField = (27, 29);
    private static readonly (int Start, int End) FinancingAmountField = (29, 41);

    /// <summary>
    /// Parses a single fixed-width line into a <see cref="BcraRecord"/>.
    /// </summary>
    /// <exception cref="FormatException">Thrown when the line width is invalid or a field cannot be parsed.</exception>
    public static BcraRecord ParseLine(ReadOnlySpan<char> line)
    {
        if (line.Length != LineWidth)
        {
            throw new FormatException(
                $"Invalid line width: expected {LineWidth} characters, got {line.Length}.");
        }

        var entityCode = Slice(line, EntityCodeField);
        var identificationType = Slice(line, IdentificationTypeField);
        var identificationNumber = Slice(line, IdentificationNumberField);
        // Field 6 is left-aligned and space-padded in the real file (e.g. "1 ", "5 ", "11").
        // Normalize to a canonical 2-digit, zero-padded code ("01", "05", "11").
        var creditStatus = NormalizeCreditStatus(line[CreditStatusField.Start..CreditStatusField.End]);
        // Field 7 is right-aligned and space-padded in the BCRA format, so trimming spaces is part
        // of parsing it (handled inside ParseFinancingAmount), unlike the fixed numeric fields above.
        var financingAmount = ParseFinancingAmount(line[FinancingAmountField.Start..FinancingAmountField.End]);

        return new BcraRecord
        {
            EntityCode = entityCode,
            IdentificationType = identificationType,
            IdentificationNumber = identificationNumber,
            CreditStatus = creditStatus,
            FinancingAmount = financingAmount
        };
    }

    /// <summary>
    /// Parses the BCRA financing amount (field 7): 11 integer digits + 1 decimal, comma as decimal
    /// separator, space-padded. Examples: "1,0", ",0", "123,4".
    /// </summary>
    /// <exception cref="FormatException">Thrown when the value cannot be parsed as a decimal.</exception>
    public static decimal ParseFinancingAmount(ReadOnlySpan<char> raw)
    {
        var trimmed = raw.Trim();

        if (trimmed.IsEmpty)
        {
            throw new FormatException("Financing amount is empty.");
        }

        // The BCRA format uses ',' as the decimal separator. InvariantCulture is immutable, so clone
        // it and force comma as the decimal point (and no group separator).
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.NumberFormat.NumberGroupSeparator = string.Empty;

        if (!decimal.TryParse(trimmed, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, culture, out var value))
        {
            throw new FormatException($"Invalid financing amount format: '{raw.ToString()}'.");
        }

        return value;
    }

    // Fields taken verbatim: entity code and identification are zero-padded/fixed in the file
    // (e.g. "00007", "20003905528"), so no transformation is needed.
    private static string Slice(ReadOnlySpan<char> line, (int Start, int End) field)
        => line[field.Start..field.End].ToString();

    /// <summary>
    /// Normalizes the credit status (field 6) to a canonical 2-digit, zero-padded code.
    /// The real file left-aligns and space-pads this field ("1 " -> "01"); 2-digit codes pass through
    /// ("11" -> "11"). The original numeric meaning is preserved.
    /// </summary>
    public static string NormalizeCreditStatus(ReadOnlySpan<char> raw)
    {
        var trimmed = raw.Trim();

        if (trimmed.IsEmpty)
        {
            throw new FormatException("Credit status is empty.");
        }

        return trimmed.ToString().PadLeft(2, '0');
    }
}

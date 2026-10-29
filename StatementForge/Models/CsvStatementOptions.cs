namespace StatementForge.Models;

/// <summary>
/// Options for parsing tabular bank statements (CSV, TSV, TXT).
/// </summary>
public sealed record CsvStatementOptions
{
    /// <summary>
    /// Column delimiter (semicolon by default).
    /// </summary>
    public string Delimiter { get; init; } = ";";

    /// <summary>
    /// Decimal separator of the amounts (comma by default).
    /// </summary>
    public string DecimalSeparator { get; init; } = ",";

    /// <summary>
    /// Expected date format (for example dd/MM/yyyy).
    /// </summary>
    public string DateFormat { get; init; } = "dd/MM/yyyy";

    /// <summary>
    /// Whether the first non-empty line is a header line to skip.
    /// </summary>
    public bool HasHeader { get; init; } = true;

    /// <summary>
    /// Index of the date column (0 by default).
    /// </summary>
    public int DateColumnIndex { get; init; }

    /// <summary>
    /// Index of the transaction label column (1 by default).
    /// </summary>
    public int LabelColumnIndex { get; init; } = 1;

    /// <summary>
    /// Index of the single signed amount column (optional when debit and credit are separate).
    /// </summary>
    public int? AmountColumnIndex { get; init; } = 2;

    /// <summary>
    /// Index of the debit amount column (optional).
    /// </summary>
    public int? DebitColumnIndex { get; init; }

    /// <summary>
    /// Index of the credit amount column (optional).
    /// </summary>
    public int? CreditColumnIndex { get; init; }

    /// <summary>
    /// Index of the transaction reference or identifier column (optional).
    /// </summary>
    public int? ReferenceColumnIndex { get; init; }
}

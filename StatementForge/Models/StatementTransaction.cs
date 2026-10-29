namespace StatementForge.Models;

/// <summary>
/// A bank transaction extracted from an account statement.
/// </summary>
public sealed record StatementTransaction
{
    /// <summary>
    /// Value date or booking date of the transaction.
    /// </summary>
    public required DateTime Date { get; init; }

    /// <summary>
    /// Absolute amount of the transaction (always positive).
    /// </summary>
    public required decimal Amount { get; init; }

    /// <summary>
    /// Direction of the transaction (debit or credit).
    /// </summary>
    public required StatementTransactionType Type { get; init; }

    /// <summary>
    /// Cleaned and normalized transaction label.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Original raw label, as found in the statement.
    /// </summary>
    public string? RawLabel { get; init; }

    /// <summary>
    /// Transaction reference or unique identifier (FITID, cheque number, and so on).
    /// </summary>
    public string? Reference { get; init; }

    /// <summary>
    /// Stable external identifier provided by the bank.
    /// </summary>
    public string? ExternalId { get; init; }

    /// <summary>
    /// Candidate category taken from the statement metadata, if any.
    /// </summary>
    public string? CategoryCandidate { get; init; }
}

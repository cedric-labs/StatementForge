namespace StatementForge.Models;

/// <summary>
/// Direction of the money flow of a bank transaction.
/// </summary>
public enum StatementTransactionType
{
    /// <summary>
    /// Debit (money out, expense).
    /// </summary>
    Debit,

    /// <summary>
    /// Credit (money in, income).
    /// </summary>
    Credit
}

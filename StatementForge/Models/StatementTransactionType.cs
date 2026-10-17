namespace StatementForge.Models;

/// <summary>
/// Indique le sens du flux financier d'une transaction bancaire.
/// </summary>
public enum StatementTransactionType
{
    /// <summary>
    /// Débit (sortie d'argent / dépense).
    /// </summary>
    Debit,

    /// <summary>
    /// Crédit (entrée d'argent / revenu).
    /// </summary>
    Credit
}

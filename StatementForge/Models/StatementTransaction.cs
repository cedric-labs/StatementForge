namespace StatementForge.Models;

/// <summary>
/// Représente une opération bancaire extraite d'un relevé de compte.
/// </summary>
public sealed record StatementTransaction
{
    /// <summary>
    /// Date de valeur ou d'enregistrement de l'opération bancaire.
    /// </summary>
    public required DateTime Date { get; init; }

    /// <summary>
    /// Montant absolu de l'opération (toujours positif).
    /// </summary>
    public required decimal Amount { get; init; }

    /// <summary>
    /// Type de flux de l'opération (Débit ou Crédit).
    /// </summary>
    public required StatementTransactionType Type { get; init; }

    /// <summary>
    /// Libellé nettoyé et normalisé de l'opération.
    /// </summary>
    public required string Label { get; init; }

    /// <summary>
    /// Libellé brut d'origine tel que présent dans le relevé.
    /// </summary>
    public string? RawLabel { get; init; }

    /// <summary>
    /// Référence ou identifiant unique d'opération (FITID, numéro de chèque, etc.).
    /// </summary>
    public string? Reference { get; init; }

    /// <summary>
    /// Identifiant externe pérenne fourni par la source bancaire.
    /// </summary>
    public string? ExternalId { get; init; }

    /// <summary>
    /// Catégorie candidate éventuellement extraite des métadonnées du relevé.
    /// </summary>
    public string? CategoryCandidate { get; init; }
}

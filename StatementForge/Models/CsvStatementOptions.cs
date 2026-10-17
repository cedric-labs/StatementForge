namespace StatementForge.Models;

/// <summary>
/// Options de configuration pour le parsing de relevés bancaires au format tabulaire CSV / TSV / TXT.
/// </summary>
public sealed record CsvStatementOptions
{
    /// <summary>
    /// Délimiteur de colonnes (point-virgule par défaut).
    /// </summary>
    public string Delimiter { get; init; } = ";";

    /// <summary>
    /// Séparateur décimal pour les montants (virgule par défaut).
    /// </summary>
    public string DecimalSeparator { get; init; } = ",";

    /// <summary>
    /// Format de date attendu (ex: dd/MM/yyyy).
    /// </summary>
    public string DateFormat { get; init; } = "dd/MM/yyyy";

    /// <summary>
    /// Indique si la première ligne non vide est une ligne d'en-tête à ignorer.
    /// </summary>
    public bool HasHeader { get; init; } = true;

    /// <summary>
    /// Index de colonne contenant la date (0 par défaut).
    /// </summary>
    public int DateColumnIndex { get; init; }

    /// <summary>
    /// Index de colonne contenant le libellé de l'opération (1 par défaut).
    /// </summary>
    public int LabelColumnIndex { get; init; } = 1;

    /// <summary>
    /// Index de colonne unique contenant le montant signé (optionnel si débit/crédit séparés).
    /// </summary>
    public int? AmountColumnIndex { get; init; } = 2;

    /// <summary>
    /// Index de colonne contenant le montant du débit (optionnel).
    /// </summary>
    public int? DebitColumnIndex { get; init; }

    /// <summary>
    /// Index de colonne contenant le montant du crédit (optionnel).
    /// </summary>
    public int? CreditColumnIndex { get; init; }

    /// <summary>
    /// Index de colonne contenant la référence ou l'identifiant d'opération (optionnel).
    /// </summary>
    public int? ReferenceColumnIndex { get; init; }
}

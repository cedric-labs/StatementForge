using StatementForge.Models;

namespace StatementForge.Parsing;

/// <summary>
/// Contrat d'abstraction pour un analyseur de relevés bancaires.
/// </summary>
public interface IBankStatementParser
{
    /// <summary>
    /// Identifiant du format bancaire supporté (ex: OFX, CSV).
    /// </summary>
    string SupportedFormat { get; }

    /// <summary>
    /// Vérifie si l'analyseur est capable d'interpréter le fichier ou le flux spécifié.
    /// </summary>
    /// <param name="fileName">Nom du fichier avec extension.</param>
    /// <param name="stream">Flux de données du fichier.</param>
    /// <returns>True si le format est reconnu et géré, sinon false.</returns>
    bool CanHandle(string fileName, Stream stream);

    /// <summary>
    /// Analyse un flux de relevé bancaire et extrait les opérations associées.
    /// </summary>
    /// <param name="stream">Flux contenant le relevé de compte.</param>
    /// <param name="options">Options de configuration optionnelles (ex: mapping pour CSV).</param>
    /// <param name="cancellationToken">Jeton d'annulation coopérative.</param>
    /// <returns>Liste en lecture seule des opérations extraites.</returns>
    Task<IReadOnlyList<StatementTransaction>> ParseAsync(
        Stream stream,
        CsvStatementOptions? options = null,
        CancellationToken cancellationToken = default);
}

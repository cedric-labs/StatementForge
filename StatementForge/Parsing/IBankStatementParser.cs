using StatementForge.Models;

namespace StatementForge.Parsing;

/// <summary>
/// Parses a bank statement format.
/// </summary>
public interface IBankStatementParser
{
    /// <summary>
    /// Identifier of the supported statement format (for example OFX, CSV).
    /// </summary>
    string SupportedFormat { get; }

    /// <summary>
    /// Checks whether this parser can read the given file or stream.
    /// </summary>
    /// <param name="fileName">File name with its extension.</param>
    /// <param name="stream">File data stream.</param>
    /// <returns>True when the format is recognized and handled; otherwise false.</returns>
    bool CanHandle(string fileName, Stream stream);

    /// <summary>
    /// Parses a bank statement stream and extracts its transactions.
    /// </summary>
    /// <param name="stream">Stream that holds the account statement.</param>
    /// <param name="options">Optional parsing options (for example the CSV column mapping).</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>Read-only list of the extracted transactions.</returns>
    Task<IReadOnlyList<StatementTransaction>> ParseAsync(
        Stream stream,
        CsvStatementOptions? options = null,
        CancellationToken cancellationToken = default);
}

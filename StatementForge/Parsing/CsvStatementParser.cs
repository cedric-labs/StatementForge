using System.Globalization;
using StatementForge.Models;

namespace StatementForge.Parsing;

/// <summary>
/// Stratégie de parsing pour les fichiers tabulaires bancaires (CSV, TSV, TXT).
/// S'adapte à la structure de chaque établissement bancaire via des options configurables.
/// </summary>
public class CsvStatementParser : IBankStatementParser
{
    /// <inheritdoc />
    public string SupportedFormat => "CSV";

    /// <inheritdoc />
    public bool CanHandle(string fileName, Stream stream)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(stream);

        var ext = Path.GetExtension(fileName);
        return ext.Equals(".csv", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".tsv", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".txt", StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StatementTransaction>> ParseAsync(
        Stream stream,
        CsvStatementOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var profile = options ?? new CsvStatementOptions();
        var transactions = new List<StatementTransaction>();
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

        string? line;
        bool isFirstLine = true;
        char delimiterChar = string.IsNullOrEmpty(profile.Delimiter) ? ';' : profile.Delimiter[0];

        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (isFirstLine && profile.HasHeader)
            {
                isFirstLine = false;
                continue;
            }

            isFirstLine = false;

            // Découpage en respectant les délimiteurs et les guillemets
            var columns = SplitCsvLine(line, delimiterChar);
            if (columns.Length <= Math.Max(profile.DateColumnIndex, profile.LabelColumnIndex))
            {
                continue; // Ligne incomplète ou ignorée
            }

            // 1. Parsing de la date
            var rawDate = columns[profile.DateColumnIndex].Trim('"', ' ');
            if (!DateTime.TryParseExact(rawDate, profile.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) &&
                !DateTime.TryParse(rawDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out date) &&
                !DateTime.TryParse(rawDate, CultureInfo.CurrentCulture, DateTimeStyles.None, out date))
            {
                continue; // Ligne d'en-tête résiduelle ou format invalide
            }

            // 2. Libellé
            var rawLabel = columns[profile.LabelColumnIndex].Trim('"', ' ');
            var label = CleanBankLabel(rawLabel);

            // 3. Montant et Type
            decimal amount = 0m;
            var type = StatementTransactionType.Debit;

            if (profile.AmountColumnIndex.HasValue && profile.AmountColumnIndex.Value < columns.Length)
            {
                var rawAmount = columns[profile.AmountColumnIndex.Value].Trim('"', ' ');
                if (TryParseAmount(rawAmount, profile.DecimalSeparator, out var parsedAmount))
                {
                    if (parsedAmount < 0)
                    {
                        type = StatementTransactionType.Debit;
                        amount = Math.Abs(parsedAmount);
                    }
                    else
                    {
                        type = StatementTransactionType.Credit;
                        amount = parsedAmount;
                    }
                }
            }
            else if (profile.DebitColumnIndex.HasValue || profile.CreditColumnIndex.HasValue)
            {
                // Gestion de colonnes séparées Débit et Crédit
                if (profile.DebitColumnIndex.HasValue && profile.DebitColumnIndex.Value < columns.Length)
                {
                    var rawDebit = columns[profile.DebitColumnIndex.Value].Trim('"', ' ');
                    if (TryParseAmount(rawDebit, profile.DecimalSeparator, out var debitVal) && debitVal != 0)
                    {
                        type = StatementTransactionType.Debit;
                        amount = Math.Abs(debitVal);
                    }
                }

                if (amount == 0 && profile.CreditColumnIndex.HasValue && profile.CreditColumnIndex.Value < columns.Length)
                {
                    var rawCredit = columns[profile.CreditColumnIndex.Value].Trim('"', ' ');
                    if (TryParseAmount(rawCredit, profile.DecimalSeparator, out var creditVal) && creditVal != 0)
                    {
                        type = StatementTransactionType.Credit;
                        amount = Math.Abs(creditVal);
                    }
                }
            }

            if (amount == 0)
            {
                continue; // Ligne sans montant valide
            }

            // 4. Référence optionnelle
            string? reference = null;
            if (profile.ReferenceColumnIndex.HasValue && profile.ReferenceColumnIndex.Value < columns.Length)
            {
                reference = columns[profile.ReferenceColumnIndex.Value].Trim('"', ' ');
            }

            transactions.Add(new StatementTransaction
            {
                Date = date,
                Amount = amount,
                Type = type,
                Label = label,
                RawLabel = rawLabel,
                Reference = reference,
                ExternalId = reference
            });
        }

        return transactions;
    }

    private static string CleanBankLabel(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "Opération";
        }

        var cleaned = raw.Replace("\t", " ", StringComparison.Ordinal).Trim();
        while (cleaned.Contains("  ", StringComparison.Ordinal))
        {
            cleaned = cleaned.Replace("  ", " ", StringComparison.Ordinal);
        }
        return cleaned;
    }

    private static bool TryParseAmount(string raw, string decimalSeparator, out decimal result)
    {
        result = 0m;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        // Nettoyage devises (€, EUR, $), espaces insécables et séparateurs de milliers
        var clean = raw.Replace("€", "", StringComparison.Ordinal)
                       .Replace("EUR", "", StringComparison.Ordinal)
                       .Replace("$", "", StringComparison.Ordinal)
                       .Replace("\u00A0", "", StringComparison.Ordinal)
                       .Replace(" ", "", StringComparison.Ordinal)
                       .Trim();

        var culture = decimalSeparator == ","
            ? new CultureInfo("fr-FR")
            : CultureInfo.InvariantCulture;

        if (decimalSeparator == "," && clean.Contains('.', StringComparison.Ordinal) && !clean.Contains(',', StringComparison.Ordinal))
        {
            clean = clean.Replace('.', ',');
        }
        else if (decimalSeparator == "." && clean.Contains(',', StringComparison.Ordinal) && !clean.Contains('.', StringComparison.Ordinal))
        {
            clean = clean.Replace(',', '.');
        }

        return decimal.TryParse(clean, NumberStyles.Number, culture, out result);
    }

    private static string[] SplitCsvLine(string line, char delimiter)
    {
        var result = new List<string>();
        bool inQuotes = false;
        var currentToken = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == delimiter && !inQuotes)
            {
                result.Add(currentToken.ToString());
                currentToken.Clear();
                continue;
            }

            currentToken.Append(c);
        }

        result.Add(currentToken.ToString());
        return result.ToArray();
    }
}

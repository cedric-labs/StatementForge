using System.Globalization;
using System.Text.RegularExpressions;
using StatementForge.Models;

namespace StatementForge.Parsing;

/// <summary>
/// Stratégie de parsing pour le format standard bancaire international OFX (Open Financial Exchange).
/// Prend en charge les formats OFX 1.x (SGML) et 2.x (XML standard) ainsi que le format Quicken QFX.
/// </summary>
public class OfxStatementParser : IBankStatementParser
{
    private static readonly Regex AccountIdRegex = new(
        @"<ACCTID>(?<acctid>.*?)(?:<\/ACCTID>|\r?\n|\Z)",
        RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(500));

    private static readonly Regex StmtTrnRegex = new(
        @"<STMTTRN>(?<block>.*?)(?=(?:<STMTTRN>|<\/BANKTRANLIST>|\Z))",
        RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    private static readonly Regex TimeZoneBracketRegex = new(
        @"\[.*?\]",
        RegexOptions.ExplicitCapture | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(250));

    /// <inheritdoc />
    public string SupportedFormat => "OFX";

    /// <summary>
    /// Extrait la valeur de la balise &lt;ACCTID&gt; depuis un fichier ou flux OFX.
    /// </summary>
    /// <param name="stream">Flux contenant le relevé OFX.</param>
    /// <param name="cancellationToken">Jeton d'annulation coopérative.</param>
    /// <returns>Identifiant du compte bancaire ou null si absent.</returns>
    public static async Task<string?> ExtractOfxAccountIdAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            if (stream.CanSeek)
            {
                var initialPos = stream.Position;
                using var reader = new StreamReader(stream, System.Text.Encoding.Latin1, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
                var content = await reader.ReadToEndAsync(cancellationToken);
                stream.Position = initialPos;

                var match = AccountIdRegex.Match(content);
                if (match.Success)
                {
                    var val = match.Groups["acctid"].Value.Trim();
                    return string.IsNullOrWhiteSpace(val) ? null : val;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ObjectDisposedException or RegexMatchTimeoutException)
        {
            return null;
        }

        return null;
    }

    /// <inheritdoc />
    public bool CanHandle(string fileName, Stream stream)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(stream);

        var ext = Path.GetExtension(fileName);
        if (ext.Equals(".ofx", StringComparison.OrdinalIgnoreCase) || ext.Equals(".qfx", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Inspection des premiers octets du flux
        try
        {
            if (stream.CanSeek)
            {
                var initialPos = stream.Position;
                using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
                var headerBuffer = new char[512];
                int read = reader.Read(headerBuffer, 0, headerBuffer.Length);
                stream.Position = initialPos;

                var text = new string(headerBuffer, 0, read);
                return text.Contains("OFXHEADER", StringComparison.OrdinalIgnoreCase) || text.Contains("<OFX>", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or ObjectDisposedException)
        {
            return false;
        }

        return false;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StatementTransaction>> ParseAsync(
        Stream stream,
        CsvStatementOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var transactions = new List<StatementTransaction>();
        using var reader = new StreamReader(stream, System.Text.Encoding.Latin1, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var content = await reader.ReadToEndAsync(cancellationToken);

        var matches = StmtTrnRegex.Matches(content);

        foreach (Match match in matches)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var block = match.Groups["block"].Value;

            var dtPosted = ExtractTagValue(block, "DTPOSTED");
            var trnAmt = ExtractTagValue(block, "TRNAMT");
            var fitId = ExtractTagValue(block, "FITID");
            var name = ExtractTagValue(block, "NAME");
            var memo = ExtractTagValue(block, "MEMO");

            if (string.IsNullOrWhiteSpace(dtPosted) || string.IsNullOrWhiteSpace(trnAmt))
            {
                continue;
            }

            // 1. Parsing de la date (Format standard OFX: YYYYMMDDHHMMSS ou YYYYMMDD)
            var date = ParseOfxDate(dtPosted);

            // 2. Montant (toujours avec '.' en standard OFX)
            if (!decimal.TryParse(trnAmt.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var amountVal))
            {
                continue;
            }

            var type = amountVal < 0 ? StatementTransactionType.Debit : StatementTransactionType.Credit;
            var absAmount = Math.Abs(amountVal);

            // 3. Libellé composé (NAME + MEMO)
            string label;
            if (!string.IsNullOrWhiteSpace(name))
            {
                label = name.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(memo))
            {
                label = memo.Trim();
            }
            else
            {
                label = "Opération OFX";
            }
            var rawLabel = $"{name} {memo}".Trim();

            // Extraction éventuelle de catégorie depuis MEMO (format standard "Catégorie - Détails")
            string? categoryCandidate = null;
            if (!string.IsNullOrEmpty(memo) && memo.Contains(" - "))
            {
                var candidate = memo.Substring(0, memo.IndexOf(" - ", StringComparison.Ordinal)).Trim();
                if (!string.IsNullOrEmpty(candidate))
                {
                    categoryCandidate = candidate;
                }
            }

            transactions.Add(new StatementTransaction
            {
                Date = date,
                Amount = absAmount,
                Type = type,
                Label = label,
                RawLabel = rawLabel,
                Reference = fitId,
                ExternalId = fitId,
                CategoryCandidate = categoryCandidate
            });
        }

        return transactions;
    }

    private static string? ExtractTagValue(string block, string tag)
    {
        // En SGML, la balise de fermeture peut être omise (<TAG>Valeur\n<NEXTTAG>)
        var match = Regex.Match(
            block,
            $@"<{tag}>(?<val>.*?)(?:<\/{tag}>|\r?\n|\Z)",
            RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
            TimeSpan.FromMilliseconds(250));

        return match.Success ? match.Groups["val"].Value.Trim() : null;
    }

    private static DateTime ParseOfxDate(string raw)
    {
        // Nettoyage de la composante fuseau horaire [0:GMT]
        var cleaned = TimeZoneBracketRegex.Replace(raw, string.Empty).Trim();
        if (cleaned.Length >= 8)
        {
            var year = int.Parse(cleaned.Substring(0, 4), CultureInfo.InvariantCulture);
            var month = int.Parse(cleaned.Substring(4, 2), CultureInfo.InvariantCulture);
            var day = int.Parse(cleaned.Substring(6, 2), CultureInfo.InvariantCulture);
            return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
        }
        return DateTime.UtcNow;
    }
}

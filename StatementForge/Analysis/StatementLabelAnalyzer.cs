using System.Text.RegularExpressions;

namespace StatementForge.Analysis;

/// <summary>
/// Analyzes bank transaction labels: payee normalization, extraction of significant
/// tokens, duplicate detection by token sets and pattern matching.
/// </summary>
public static class StatementLabelAnalyzer
{
    private static readonly TimeSpan s_regexTimeout = TimeSpan.FromMilliseconds(250);

    private static readonly HashSet<string> s_contractKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "AUTO", "VOITURE", "VEHICULE", "HABITATION", "LOGEMENT", "SANTE", "MUTUELLE", "PREVOYANCE", "VIE",
        "BOX", "FIBRE", "MOBILE", "INTERNET", "ELEC", "ELECTRICITE", "GAZ", "LOYER"
    };

    private static readonly HashSet<string> s_genericBankingWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "paiement", "carte", "prelevement", "virement", "votre", "faveur", "emis", "web", "retrait", "distributeur",
        "operation", "sepa", "facture", "solde", "cheque", "vir", "prlvt", "cotis", "frais"
    };

    /// <summary>
    /// Checks whether a transaction amount matches the expected amount within a relative tolerance.
    /// </summary>
    /// <param name="expectedAmount">Expected reference amount.</param>
    /// <param name="tolerancePercentage">Relative tolerance (for example <c>0.05m</c> for 5%).</param>
    /// <param name="transactionAmount">Actual transaction amount.</param>
    /// <returns><c>true</c> when the amount is within the tolerance; otherwise <c>false</c>.</returns>
    public static bool MatchesAmount(decimal expectedAmount, decimal tolerancePercentage, decimal transactionAmount)
    {
        if (expectedAmount <= 0)
        {
            return true;
        }

        if (tolerancePercentage <= 0)
        {
            return Math.Abs(transactionAmount - expectedAmount) <= 0.01m;
        }

        var delta = expectedAmount * tolerancePercentage;
        var min = expectedAmount - delta;
        var max = expectedAmount + delta;
        return transactionAmount >= min && transactionAmount <= max;
    }

    /// <summary>
    /// Checks whether the label or raw label of a transaction matches a pattern (substring, <c>*</c>/<c>%</c> wildcard or regular expression).
    /// </summary>
    /// <param name="label">Main transaction label.</param>
    /// <param name="rawLabel">Optional raw label.</param>
    /// <param name="pattern">Search pattern.</param>
    /// <returns><c>true</c> when the pattern matches; otherwise <c>false</c>.</returns>
    public static bool MatchesPattern(string label, string? rawLabel, string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return false;
        }

        var cleanP = pattern.Trim();
        var safeLabel = label ?? string.Empty;
        var fullText = $"{safeLabel} {rawLabel ?? string.Empty}".Trim();

        if (safeLabel.Contains(cleanP, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrEmpty(rawLabel) && rawLabel.Contains(cleanP, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (cleanP.Contains('*', StringComparison.Ordinal) || cleanP.Contains('%', StringComparison.Ordinal))
        {
            try
            {
                var escaped = Regex.Escape(cleanP);
                var regexPattern = escaped
                    .Replace(@"\*", ".*", StringComparison.Ordinal)
                    .Replace(@"\%", ".*", StringComparison.Ordinal)
                    .Replace("%", ".*", StringComparison.Ordinal);

                if (Regex.IsMatch(fullText, regexPattern, RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, TimeSpan.FromMilliseconds(200)))
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
            {
                return false;
            }
        }

        try
        {
            if (Regex.IsMatch(fullText, cleanP, RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture, TimeSpan.FromMilliseconds(200)))
            {
                return true;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
        {
            return false;
        }

        return false;
    }

    /// <summary>
    /// Strips the noisy bank prefixes (<c>PRLV</c>, <c>VIR</c>, <c>CB</c>, dates, references)
    /// to extract the normalized payee and its contract descriptor, if any.
    /// </summary>
    /// <param name="label">Main transaction label.</param>
    /// <param name="rawLabel">Optional raw label.</param>
    /// <returns>Normalized payee key, in upper case.</returns>
    public static string CleanTransactionLabel(string label, string? rawLabel)
    {
        var combined = $"{label} {rawLabel ?? string.Empty}".Trim();
        if (string.IsNullOrWhiteSpace(combined))
        {
            return string.Empty;
        }

        var text = combined.ToUpperInvariant();

        text = Regex.Replace(
            text,
            @"\b(?:PRLV SEPA|VIR SEPA|PAIEMENT PAR CARTE|PRELEVEMENT|VIREMENT|PRLV|VIR|CARTE|CB|COTIS|COTISATION|SEPA)\b",
            " ",
            RegexOptions.ExplicitCapture,
            s_regexTimeout);
        text = Regex.Replace(text, @"^(?:DE|DU|POUR)\s+", " ", RegexOptions.ExplicitCapture, s_regexTimeout);
        text = Regex.Replace(text, @"\b\d{1,2}[/\.\-]\d{1,2}(?:[/\.\-]\d{2,4})?\b", " ", RegexOptions.ExplicitCapture, s_regexTimeout);
        text = Regex.Replace(text, @"\b\d{5,}\b", " ", RegexOptions.None, s_regexTimeout);
        text = Regex.Replace(text, @"\b(?:FACTURE|EMETTEUR|MANDAT|ID|NUM|NO)\b", " ", RegexOptions.ExplicitCapture, s_regexTimeout);
        text = Regex.Replace(text, @"[_\*#:\(\)\[\]/\\;,]", " ", RegexOptions.None, s_regexTimeout);
        text = Regex.Replace(text, @"\s+", " ", RegexOptions.None, s_regexTimeout).Trim();

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 2 && !Regex.IsMatch(w, @"^\d+$", RegexOptions.None, s_regexTimeout))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (words.Count == 0)
        {
            return string.Empty;
        }

        var firstWord = words[0];
        var descriptor = words.Skip(1).FirstOrDefault(w => s_contractKeywords.Contains(w));
        if (descriptor is not null)
        {
            return $"{firstWord} {descriptor}";
        }

        return words.Count >= 2 ? $"{words[0]} {words[1]}" : words[0];
    }

    /// <summary>
    /// Formats a normalized payee pattern as a readable suggested name (Title Case).
    /// </summary>
    /// <param name="pattern">Normalized pattern.</param>
    /// <returns>The name with each word capitalized.</returns>
    public static string FormatSuggestedName(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return string.Empty;
        }

        var parts = pattern.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 0)
            {
                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i][1..];
            }
        }

        return string.Join(' ', parts);
    }

    /// <summary>
    /// Extracts the significant alphanumeric tokens of a bank label,
    /// leaving out card masks, dates and generic banking vocabulary.
    /// </summary>
    /// <param name="label">Main label.</param>
    /// <param name="rawLabel">Optional raw label.</param>
    /// <returns>Case-insensitive set of the significant tokens.</returns>
    public static HashSet<string> ExtractMeaningfulTokens(string? label, string? rawLabel)
    {
        var text = $"{label} {rawLabel}".Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        text = Regex.Replace(text, @"\bX\d{4}\b", string.Empty, RegexOptions.IgnoreCase, s_regexTimeout);
        text = Regex.Replace(text, @"\b\d{2}/\d{2}(?:/\d{2,4})?\b", string.Empty, RegexOptions.ExplicitCapture, s_regexTimeout);

        var words = Regex.Matches(text, @"[a-zA-Z0-9]{3,}", RegexOptions.None, s_regexTimeout)
            .Select(m => m.Value.ToLowerInvariant())
            .Where(w => !s_genericBankingWords.Contains(w));

        return new HashSet<string>(words, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the tokens of a candidate transaction match the label of an existing transaction.
    /// </summary>
    /// <param name="candidateTokens">Tokens extracted from the candidate transaction.</param>
    /// <param name="existingLabel">Label of the existing transaction.</param>
    /// <param name="existingRawLabel">Raw label of the existing transaction.</param>
    /// <param name="sameDate">Whether both transactions have exactly the same date.</param>
    /// <returns><c>true</c> when the labels are considered similar; otherwise <c>false</c>.</returns>
    public static bool IsLabelMatching(
        IReadOnlySet<string> candidateTokens,
        string? existingLabel,
        string? existingRawLabel,
        bool sameDate)
    {
        var existingTokens = ExtractMeaningfulTokens(existingLabel, existingRawLabel);
        return IsTokenSetMatching(candidateTokens, existingTokens, sameDate);
    }

    /// <summary>
    /// Determines whether two sets of significant tokens overlap enough to be considered duplicates.
    /// </summary>
    /// <param name="tokensA">First token set.</param>
    /// <param name="tokensB">Second token set.</param>
    /// <param name="sameDate">Whether both transactions have exactly the same date.</param>
    /// <returns><c>true</c> when the sets intersect, or when both are empty on the same date.</returns>
    public static bool IsTokenSetMatching(
        IReadOnlySet<string> tokensA,
        IReadOnlySet<string> tokensB,
        bool sameDate)
    {
        ArgumentNullException.ThrowIfNull(tokensA);
        ArgumentNullException.ThrowIfNull(tokensB);

        if (tokensA.Count == 0 && tokensB.Count == 0)
        {
            return sameDate;
        }

        return tokensA.Overlaps(tokensB);
    }
}

using System.Text.RegularExpressions;

namespace StatementForge.Analysis;

/// <summary>
/// Analyseur de libellés bancaires fournissant la normalisation d'émetteur, l'extraction de jetons
/// significatifs, la détection de doublons par ensemble de jetons et la correspondance de motifs.
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
    /// Vérifie si un montant d'opération correspond au montant attendu selon une tolérance relative.
    /// </summary>
    /// <param name="expectedAmount">Montant de référence attendu.</param>
    /// <param name="tolerancePercentage">Tolérance relative (ex: <c>0.05m</c> pour 5%).</param>
    /// <param name="transactionAmount">Montant réel de la transaction.</param>
    /// <returns><c>true</c> si le montant est compris dans l'intervalle de tolérance ; sinon, <c>false</c>.</returns>
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
    /// Vérifie si le libellé ou le libellé brut d'une opération correspond à un motif (sous-chaîne, joker <c>*</c>/<c>%</c> ou expression régulière).
    /// </summary>
    /// <param name="label">Libellé principal de l'opération.</param>
    /// <param name="rawLabel">Libellé brut optionnel.</param>
    /// <param name="pattern">Motif de recherche.</param>
    /// <returns><c>true</c> si le motif correspond ; sinon, <c>false</c>.</returns>
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
    /// Nettoie les préfixes bancaires bruyants (<c>PRLV</c>, <c>VIR</c>, <c>CB</c>, dates, références)
    /// pour extraire l'émetteur normalisé et son descripteur de contrat éventuel.
    /// </summary>
    /// <param name="label">Libellé principal de l'opération.</param>
    /// <param name="rawLabel">Libellé brut optionnel.</param>
    /// <returns>Clé d'émetteur normalisée en majuscules.</returns>
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
    /// Formate un motif d'émetteur normalisé en nom suggéré lisible (Title Case).
    /// </summary>
    /// <param name="pattern">Motif normalisé.</param>
    /// <returns>Nom formaté avec initiale majuscule sur chaque mot.</returns>
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
    /// Extrait l'ensemble des jetons alphanumériques significatifs d'un libellé bancaire
    /// en filtrant les masques de carte, les dates et le vocabulaire bancaire générique.
    /// </summary>
    /// <param name="label">Libellé principal.</param>
    /// <param name="rawLabel">Libellé brut optionnel.</param>
    /// <returns>Ensemble insensible à la casse des jetons significatifs.</returns>
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
    /// Détermine si les jetons d'une opération candidate correspondent au libellé d'une opération existante.
    /// </summary>
    /// <param name="candidateTokens">Jetons extraits de l'opération candidate.</param>
    /// <param name="existingLabel">Libellé de l'opération existante.</param>
    /// <param name="existingRawLabel">Libellé brut de l'opération existante.</param>
    /// <param name="sameDate">Indique si les deux opérations portent exactement la même date.</param>
    /// <returns><c>true</c> si les libellés sont considérés similaires ; sinon, <c>false</c>.</returns>
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
    /// Détermine si deux ensembles de jetons significatifs présentent un recouvrement suffisant pour être considérés comme doublons.
    /// </summary>
    /// <param name="tokensA">Premier ensemble de jetons.</param>
    /// <param name="tokensB">Second ensemble de jetons.</param>
    /// <param name="sameDate">Indique si les deux opérations portent exactement la même date.</param>
    /// <returns><c>true</c> s'il existe une intersection non vide ou si les deux ensembles sont vides à la même date.</returns>
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

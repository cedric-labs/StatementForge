namespace StatementForge.Analysis;

/// <summary>
/// Fréquence de récurrence détectée pour une série d'opérations bancaires.
/// </summary>
public enum StatementRecurrenceFrequency
{
    /// <summary>Fréquence mensuelle (~30 jours).</summary>
    Monthly = 0,

    /// <summary>Fréquence trimestrielle (~90 jours).</summary>
    Quarterly = 1,

    /// <summary>Fréquence semestrielle (~180 jours).</summary>
    Biannual = 2,

    /// <summary>Fréquence annuelle (~365 jours).</summary>
    Annual = 3
}

/// <summary>
/// Analyseur statistique de cadence temporelle et de régularité des montants pour la détection de charges récurrentes.
/// </summary>
public static class RecurringCadenceAnalyzer
{
    /// <summary>
    /// Détermine si une échéance récurrente s'applique à un mois calendaire donné (1 à 12).
    /// </summary>
    /// <param name="frequency">Fréquence de la charge récurrente.</param>
    /// <param name="month">Numéro du mois (1 à 12).</param>
    /// <returns><c>true</c> si la charge est attendue sur ce mois ; sinon, <c>false</c>.</returns>
    public static bool AppliesToMonth(StatementRecurrenceFrequency frequency, int month) =>
        frequency switch
        {
            StatementRecurrenceFrequency.Monthly => true,
            StatementRecurrenceFrequency.Quarterly => (month % 3 == 0) || month is 1 or 4 or 7 or 10,
            StatementRecurrenceFrequency.Biannual => month is 1 or 6 or 7 or 12,
            StatementRecurrenceFrequency.Annual => true,
            _ => true
        };

    /// <summary>
    /// Analyse une liste ordonnée de dates d'opérations et détecte la fréquence probable ainsi que la régularité de la cadence.
    /// </summary>
    /// <param name="orderedDates">Dates d'opérations triées par ordre chronologique.</param>
    /// <returns>Un tuple indiquant si une cadence valide est détectée, la fréquence et la régularité.</returns>
    public static (bool IsDetected, StatementRecurrenceFrequency Frequency, bool IsRegularCadence) DetectCadence(IReadOnlyList<DateTime> orderedDates)
    {
        ArgumentNullException.ThrowIfNull(orderedDates);
        if (orderedDates.Count < 2)
        {
            return (false, StatementRecurrenceFrequency.Monthly, false);
        }

        var intervals = new List<int>();
        for (int i = 1; i < orderedDates.Count; i++)
        {
            var days = (orderedDates[i] - orderedDates[i - 1]).Days;
            if (days > 0)
            {
                intervals.Add(days);
            }
        }

        if (intervals.Count == 0)
        {
            return (false, StatementRecurrenceFrequency.Monthly, false);
        }

        var avgInterval = intervals.Average();
        if (avgInterval is >= 25 and <= 36)
        {
            var isRegular = intervals.TrueForAll(d => d is >= 20 and <= 45);
            return (true, StatementRecurrenceFrequency.Monthly, isRegular);
        }

        if (avgInterval is >= 80 and <= 100)
        {
            var isRegular = intervals.TrueForAll(d => d is >= 75 and <= 110);
            return (true, StatementRecurrenceFrequency.Quarterly, isRegular);
        }

        if (avgInterval is >= 340 and <= 390)
        {
            return (true, StatementRecurrenceFrequency.Annual, true);
        }

        return (false, StatementRecurrenceFrequency.Monthly, false);
    }

    /// <summary>
    /// Calcule un score de confiance (de 50 à 98) pour un groupe d'opérations récurrentes candidates.
    /// </summary>
    /// <param name="occurrencesCount">Nombre d'occurrences observées.</param>
    /// <param name="minAmount">Montant minimal observé.</param>
    /// <param name="maxAmount">Montant maximal observé.</param>
    /// <returns>Score de confiance borné entre 50 et 98.</returns>
    public static int CalculateConfidenceScore(int occurrencesCount, decimal minAmount, decimal maxAmount)
    {
        int score = 50;
        if (occurrencesCount >= 3)
        {
            score += 20;
        }

        if (occurrencesCount >= 5)
        {
            score += 15;
        }

        if (Math.Abs(maxAmount - minAmount) < 0.10m)
        {
            score += 15;
        }

        return Math.Min(98, score);
    }
}

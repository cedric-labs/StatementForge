namespace StatementForge.Analysis;

/// <summary>
/// Recurrence frequency detected for a series of bank transactions.
/// </summary>
public enum StatementRecurrenceFrequency
{
    /// <summary>Monthly (~30 days).</summary>
    Monthly = 0,

    /// <summary>Quarterly (~90 days).</summary>
    Quarterly = 1,

    /// <summary>Half-yearly (~180 days).</summary>
    Biannual = 2,

    /// <summary>Yearly (~365 days).</summary>
    Annual = 3
}

/// <summary>
/// Measures the time cadence and amount regularity of transactions to detect recurring charges.
/// </summary>
public static class RecurringCadenceAnalyzer
{
    /// <summary>
    /// Determines whether a recurring charge falls due in a given calendar month (1 to 12).
    /// </summary>
    /// <param name="frequency">Frequency of the recurring charge.</param>
    /// <param name="month">Month number (1 to 12).</param>
    /// <returns><c>true</c> when the charge is expected that month; otherwise <c>false</c>.</returns>
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
    /// Analyzes an ordered list of transaction dates and detects the likely frequency and how regular the cadence is.
    /// </summary>
    /// <param name="orderedDates">Transaction dates in chronological order.</param>
    /// <returns>A tuple telling whether a valid cadence was detected, with its frequency and regularity.</returns>
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
    /// Computes a confidence score (50 to 98) for a group of candidate recurring transactions.
    /// </summary>
    /// <param name="occurrencesCount">Number of observed occurrences.</param>
    /// <param name="minAmount">Smallest observed amount.</param>
    /// <param name="maxAmount">Largest observed amount.</param>
    /// <returns>Confidence score between 50 and 98.</returns>
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

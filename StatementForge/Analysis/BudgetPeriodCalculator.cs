namespace StatementForge.Analysis;

/// <summary>
/// Calculateur de périodes glissantes, mensuelles et salariales pour le filtrage d'opérations bancaires.
/// </summary>
public static class BudgetPeriodCalculator
{
    /// <summary>Code de la période du mois en cours.</summary>
    public const string CurrentMonth = "current-month";

    /// <summary>Code de la période des 14 derniers jours.</summary>
    public const string TwoWeeks = "two-weeks";

    /// <summary>Code de la période du mois précédent.</summary>
    public const string LastMonth = "last-month";

    /// <summary>Code de la période des 3 derniers mois.</summary>
    public const string ThreeMonths = "three-months";

    /// <summary>Code de la période des 6 derniers mois.</summary>
    public const string SixMonths = "six-months";

    /// <summary>Code désignant l'intégralité de l'historique sans borne de date.</summary>
    public const string All = "all";

    /// <summary>Code d'une plage de dates personnalisée.</summary>
    public const string Custom = "custom";

    /// <summary>
    /// Résout la période et les dates de début/fin en fonction des paramètres transmis.
    /// </summary>
    /// <param name="requestedPeriod">Identifiant de période demandé.</param>
    /// <param name="requestedStartDate">Date de début manuelle optionnelle.</param>
    /// <param name="requestedEndDate">Date de fin manuelle optionnelle.</param>
    /// <param name="defaultPeriodFallback">Période par défaut si non spécifiée.</param>
    /// <param name="salaryMonthStartDay">Jour du mois de début du cycle budgétaire/salarial (1 à 28).</param>
    /// <param name="today">Date de référence optionnelle (par défaut <see cref="DateTime.Today"/>).</param>
    /// <returns>Un tuple contenant la date de début, la date de fin et le code de période résolu.</returns>
    public static (DateTime? StartDate, DateTime? EndDate, string Period) ResolvePeriod(
        string? requestedPeriod,
        DateTime? requestedStartDate,
        DateTime? requestedEndDate,
        string? defaultPeriodFallback = CurrentMonth,
        int salaryMonthStartDay = 1,
        DateTime? today = null)
    {
        var refToday = today ?? DateTime.Today;

        if (string.Equals(requestedPeriod, All, StringComparison.OrdinalIgnoreCase))
        {
            return (null, null, All);
        }

        if (requestedStartDate.HasValue || requestedEndDate.HasValue)
        {
            var detected = DetectPreset(requestedStartDate, requestedEndDate, refToday, salaryMonthStartDay);
            return (requestedStartDate, requestedEndDate, detected ?? requestedPeriod ?? Custom);
        }

        var targetPeriod = requestedPeriod?.ToLowerInvariant() ?? defaultPeriodFallback?.ToLowerInvariant() ?? CurrentMonth;
        return targetPeriod switch
        {
            TwoWeeks => (refToday.AddDays(-14), refToday, TwoWeeks),
            LastMonth => GetLastMonthRange(refToday, salaryMonthStartDay),
            ThreeMonths => (refToday.AddMonths(-3), refToday, ThreeMonths),
            SixMonths => (refToday.AddMonths(-6), refToday, SixMonths),
            All => (null, null, All),
            _ => GetCurrentMonthRange(refToday, salaryMonthStartDay)
        };
    }

    /// <summary>
    /// Calcule les bornes du mois courant (calendaire ou salarial glissant).
    /// </summary>
    /// <param name="today">Date de référence.</param>
    /// <param name="salaryMonthStartDay">Jour de début du cycle salarial (1 à 28).</param>
    /// <returns>Bornes de début et de fin du mois en cours.</returns>
    public static (DateTime? StartDate, DateTime? EndDate, string Period) GetCurrentMonthRange(DateTime today, int salaryMonthStartDay = 1)
    {
        if (salaryMonthStartDay is <= 1 or > 28)
        {
            var start = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month), 0, 0, 0, DateTimeKind.Utc);
            return (start, end, CurrentMonth);
        }

        var periodStart = today.Day >= salaryMonthStartDay
            ? new DateTime(today.Year, today.Month, salaryMonthStartDay, 0, 0, 0, DateTimeKind.Utc)
            : new DateTime(today.Year, today.Month, salaryMonthStartDay, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);

        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        return (periodStart, periodEnd, CurrentMonth);
    }

    /// <summary>
    /// Calcule les bornes du mois précédent (calendaire ou salarial glissant).
    /// </summary>
    /// <param name="today">Date de référence.</param>
    /// <param name="salaryMonthStartDay">Jour de début du cycle salarial (1 à 28).</param>
    /// <returns>Bornes de début et de fin du mois précédent.</returns>
    public static (DateTime? StartDate, DateTime? EndDate, string Period) GetLastMonthRange(DateTime today, int salaryMonthStartDay = 1)
    {
        if (salaryMonthStartDay is <= 1 or > 28)
        {
            var prevMonth = today.AddMonths(-1);
            var start = new DateTime(prevMonth.Year, prevMonth.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(prevMonth.Year, prevMonth.Month, DateTime.DaysInMonth(prevMonth.Year, prevMonth.Month), 0, 0, 0, DateTimeKind.Utc);
            return (start, end, LastMonth);
        }

        var (currentStart, _, _) = GetCurrentMonthRange(today, salaryMonthStartDay);
        if (currentStart is not { } validStart)
        {
            return (null, null, LastMonth);
        }

        var periodStart = validStart.AddMonths(-1);
        var periodEnd = validStart.AddDays(-1);
        return (periodStart, periodEnd, LastMonth);
    }

    private static string? DetectPreset(DateTime? start, DateTime? end, DateTime today, int salaryMonthStartDay = 1)
    {
        if (!start.HasValue || !end.HasValue)
        {
            return null;
        }

        var (curStart, curEnd, _) = GetCurrentMonthRange(today, salaryMonthStartDay);
        if (start.Value.Date == curStart && end.Value.Date == curEnd)
        {
            return CurrentMonth;
        }

        var (prevStart, prevEnd, _) = GetLastMonthRange(today, salaryMonthStartDay);
        if (start.Value.Date == prevStart && end.Value.Date == prevEnd)
        {
            return LastMonth;
        }

        if (start.Value.Date == today.AddDays(-14) && end.Value.Date == today)
        {
            return TwoWeeks;
        }

        if (start.Value.Date == today.AddMonths(-3) && end.Value.Date == today)
        {
            return ThreeMonths;
        }

        if (start.Value.Date == today.AddMonths(-6) && end.Value.Date == today)
        {
            return SixMonths;
        }

        return Custom;
    }
}

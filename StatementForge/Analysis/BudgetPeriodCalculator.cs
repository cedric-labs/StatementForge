namespace StatementForge.Analysis;

/// <summary>
/// Computes rolling, calendar-month and salary-month periods used to filter bank transactions.
/// </summary>
public static class BudgetPeriodCalculator
{
    /// <summary>Code of the current month period.</summary>
    public const string CurrentMonth = "current-month";

    /// <summary>Code of the last 14 days period.</summary>
    public const string TwoWeeks = "two-weeks";

    /// <summary>Code of the previous month period.</summary>
    public const string LastMonth = "last-month";

    /// <summary>Code of the last 3 months period.</summary>
    public const string ThreeMonths = "three-months";

    /// <summary>Code of the last 6 months period.</summary>
    public const string SixMonths = "six-months";

    /// <summary>Code of the whole history, with no date bound.</summary>
    public const string All = "all";

    /// <summary>Code of a custom date range.</summary>
    public const string Custom = "custom";

    /// <summary>
    /// Resolves the period and its start and end dates from the given parameters.
    /// </summary>
    /// <param name="requestedPeriod">Requested period code.</param>
    /// <param name="requestedStartDate">Optional manual start date.</param>
    /// <param name="requestedEndDate">Optional manual end date.</param>
    /// <param name="defaultPeriodFallback">Period used when none is requested.</param>
    /// <param name="salaryMonthStartDay">Day of the month on which the budget or salary cycle starts (1 to 28).</param>
    /// <param name="today">Optional reference date (defaults to <see cref="DateTime.Today"/>).</param>
    /// <returns>A tuple with the start date, the end date and the resolved period code.</returns>
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
    /// Computes the bounds of the current month (calendar or rolling salary month).
    /// </summary>
    /// <param name="today">Reference date.</param>
    /// <param name="salaryMonthStartDay">Day the salary cycle starts (1 to 28).</param>
    /// <returns>Start and end bounds of the current month.</returns>
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
    /// Computes the bounds of the previous month (calendar or rolling salary month).
    /// </summary>
    /// <param name="today">Reference date.</param>
    /// <param name="salaryMonthStartDay">Day the salary cycle starts (1 to 28).</param>
    /// <returns>Start and end bounds of the previous month.</returns>
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

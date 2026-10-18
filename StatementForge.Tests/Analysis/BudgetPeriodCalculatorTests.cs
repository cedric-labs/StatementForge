using FluentAssertions;
using StatementForge.Analysis;
using Xunit;

namespace StatementForge.Tests.Analysis;

public sealed class BudgetPeriodCalculatorTests
{
    [Fact]
    public void ResolvePeriod_WhenAllRequested_ReturnsNullBounds()
    {
        var (start, end, period) = BudgetPeriodCalculator.ResolvePeriod(
            BudgetPeriodCalculator.All,
            null,
            null);

        start.Should().BeNull();
        end.Should().BeNull();
        period.Should().Be(BudgetPeriodCalculator.All);
    }

    [Fact]
    public void GetCurrentMonthRange_WhenCalendarMonth_ReturnsFirstAndLastDayOfMonth()
    {
        var refDate = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc);
        var (start, end, period) = BudgetPeriodCalculator.GetCurrentMonthRange(refDate, salaryMonthStartDay: 1);

        start.Should().Be(new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc));
        period.Should().Be(BudgetPeriodCalculator.CurrentMonth);
    }

    [Fact]
    public void GetCurrentMonthRange_WhenSalaryStartDayIs25AndBeforeOrAfter25_ReturnsExpectedSalaryCycle()
    {
        var before25 = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);
        var (start1, end1, _) = BudgetPeriodCalculator.GetCurrentMonthRange(before25, salaryMonthStartDay: 25);

        start1.Should().Be(new DateTime(2026, 3, 25, 0, 0, 0, DateTimeKind.Utc));
        end1.Should().Be(new DateTime(2026, 4, 24, 0, 0, 0, DateTimeKind.Utc));

        var after25 = new DateTime(2026, 4, 26, 0, 0, 0, DateTimeKind.Utc);
        var (start2, end2, _) = BudgetPeriodCalculator.GetCurrentMonthRange(after25, salaryMonthStartDay: 25);

        start2.Should().Be(new DateTime(2026, 4, 25, 0, 0, 0, DateTimeKind.Utc));
        end2.Should().Be(new DateTime(2026, 5, 24, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void GetLastMonthRange_WhenCalendarOrSalaryStartDay_ReturnsPreviousCycle()
    {
        var refDate = new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc);
        var (startCal, endCal, _) = BudgetPeriodCalculator.GetLastMonthRange(refDate, salaryMonthStartDay: 1);
        startCal.Should().Be(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        endCal.Should().Be(new DateTime(2026, 3, 31, 0, 0, 0, DateTimeKind.Utc));

        var (startSal, endSal, period) = BudgetPeriodCalculator.GetLastMonthRange(refDate, salaryMonthStartDay: 25);
        startSal.Should().Be(new DateTime(2026, 2, 25, 0, 0, 0, DateTimeKind.Utc));
        endSal.Should().Be(new DateTime(2026, 3, 24, 0, 0, 0, DateTimeKind.Utc));
        period.Should().Be(BudgetPeriodCalculator.LastMonth);
    }

    [Fact]
    public void ResolvePeriod_WhenPresetsOrCustomDatesRequested_ComputesExpectedRangesAndDetectsPresets()
    {
        var today = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);

        var (s2w, e2w, p2w) = BudgetPeriodCalculator.ResolvePeriod(BudgetPeriodCalculator.TwoWeeks, null, null, today: today);
        s2w.Should().Be(today.AddDays(-14));
        e2w.Should().Be(today);
        p2w.Should().Be(BudgetPeriodCalculator.TwoWeeks);

        var (slm, _, plm) = BudgetPeriodCalculator.ResolvePeriod(BudgetPeriodCalculator.LastMonth, null, null, today: today);
        slm.Should().Be(new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        plm.Should().Be(BudgetPeriodCalculator.LastMonth);

        var (s3m, _, p3m) = BudgetPeriodCalculator.ResolvePeriod(BudgetPeriodCalculator.ThreeMonths, null, null, today: today);
        s3m.Should().Be(today.AddMonths(-3));
        p3m.Should().Be(BudgetPeriodCalculator.ThreeMonths);

        var (s6m, _, p6m) = BudgetPeriodCalculator.ResolvePeriod(BudgetPeriodCalculator.SixMonths, null, null, today: today);
        s6m.Should().Be(today.AddMonths(-6));
        p6m.Should().Be(BudgetPeriodCalculator.SixMonths);

        var (_, _, pCustom) = BudgetPeriodCalculator.ResolvePeriod(null, today.AddDays(-5), today, today: today);
        pCustom.Should().Be(BudgetPeriodCalculator.Custom);

        var (_, _, pDetected2w) = BudgetPeriodCalculator.ResolvePeriod(null, today.AddDays(-14), today, today: today);
        pDetected2w.Should().Be(BudgetPeriodCalculator.TwoWeeks);
    }
}

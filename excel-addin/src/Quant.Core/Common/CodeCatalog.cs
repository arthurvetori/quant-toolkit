namespace Quant.Core.Common;

public static class CodeCatalog
{
    public static IReadOnlyList<CodeDescription> Calendars { get; } =
    [
        new(0, "Brazil Settlement", "Brazil settlement calendar with code-maintained holiday corrections."),
        new(1, "US Settlement", "United States settlement calendar."),
        new(2, "Brazil + US", "Joint calendar; both Brazil and US settlement markets must be open.")
    ];

    public static IReadOnlyList<CodeDescription> DayCounters { get; } =
    [
        new(0, "Business/252", "Business days divided by 252 using the selected calendar."),
        new(1, "Actual/365 Fixed", "Actual calendar days divided by 365."),
        new(2, "30/360 Bond Basis", "Thirty/360 using QuantLib Bond Basis rules."),
        new(3, "Actual/360", "Actual calendar days divided by 360."),
        new(4, "Actual/365 No Leap", "Actual/365 excluding leap days."),
        new(5, "Actual/Actual ISDA", "Actual/Actual using ISDA rules."),
        new(6, "Actual/Actual AFB", "Actual/Actual using AFB rules."),
        new(7, "30/360 USA", "Thirty/360 using US rules."),
        new(8, "30/360 European", "Thirty/360 using European rules."),
        new(9, "30/360 Italian", "Thirty/360 using Italian rules."),
        new(10, "30/360 NASD", "Thirty/360 using NASD rules."),
        new(11, "One Day", "QuantLib One Day convention."),
        new(12, "Simple", "QuantLib Simple convention.")
    ];

    public static IReadOnlyList<CodeDescription> BusinessDayConventions { get; } =
    [
        new(0, "Modified Following", "Move forward unless that changes month, then move backward."),
        new(1, "Following", "Move to the next business day."),
        new(2, "Preceding", "Move to the previous business day."),
        new(3, "Modified Preceding", "Move backward unless that changes month, then move forward."),
        new(4, "Unadjusted", "Do not adjust the date."),
        new(5, "Half-Month Modified Following", "QuantLib half-month modified-following adjustment."),
        new(6, "Nearest", "Move to the nearest business day.")
    ];

    public static IReadOnlyList<CodeDescription> TimeUnits { get; } =
    [
        new(0, "Months", "Interval measured in months."),
        new(1, "Years", "Interval measured in years."),
        new(2, "Weeks", "Interval measured in weeks."),
        new(3, "Days", "Interval measured in days.")
    ];
}

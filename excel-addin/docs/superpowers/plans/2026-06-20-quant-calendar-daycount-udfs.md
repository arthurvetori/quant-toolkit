# Quant Calendar and Day-Count UDFs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver tested, documented Excel functions for calendar discovery, date adjustment, business-day calculations, QuantLib day counts, year fractions, and backward payment schedules.

**Architecture:** Thin Excel-DNA facades convert Excel values and integer codes, then call typed services in `Quant.Core`. `Quant.QuantLib` implements those interfaces with session-lived, immutable-after-initialization official QuantLib objects and explicit switch-based catalogs.

**Tech Stack:** C# 12, .NET 8, ExcelDna.AddIn 1.9.0, official QuantLib/QuantLib-SWIG 1.42.1, xUnit 2.9.3.

## Global Constraints

- Complete the foundation/native-build plan first.
- Use QuantLib for every financial date calculation, including Business/252 and every 30/360 variant.
- Public Excel inputs use stable integer codes; do not expose C# or QuantLib enum ordinals.
- Calendar IDs are `0` Brazil Settlement, `1` US Settlement, and `2` Brazil + US `JoinHolidays`.
- Day-counter IDs `0`, `1`, and `2` are Business/252, Actual/365 Fixed, and 30/360 Bond Basis.
- Business-day convention ID `0` is Modified Following and is the optional default.
- `bBDays` defaults to `includeStart=false` and `includeEnd=true`.
- Month/year advancement and schedules always preserve end-of-month relationships.
- Worksheet calculations return their expected type or native Excel errors, never descriptive error strings.
- Every exported UDF and public core operation requires tests and full function/argument descriptions.
- Keep successful calculations free of reflection, string parsing, native-object allocation, file I/O, and logging.
- Preserve unrelated staged or working-tree changes; path-limit every commit.

---

### Task 1: Define stable codes and discovery metadata

**Files:**
- Create: `src/Quant.Core/Common/CodeDescription.cs`
- Create: `src/Quant.Core/Calendars/CalendarCode.cs`
- Create: `src/Quant.Core/Calendars/BusinessDayConventionCode.cs`
- Create: `src/Quant.Core/Calendars/TimeUnitCode.cs`
- Create: `src/Quant.Core/DayCounters/DayCounterCode.cs`
- Create: `src/Quant.Core/Common/CodeCatalog.cs`
- Create: `tests/Quant.Core.Tests/Common/CodeCatalogTests.cs`
- Create: `docs/functions/codes.md`

**Interfaces:**
- Consumes: `Quant.Core` from the foundation plan.
- Produces: `CalendarCode`, `BusinessDayConventionCode`, `TimeUnitCode`, `DayCounterCode`, and `CodeCatalog` used by every later task.

- [ ] **Step 1: Write tests that lock all public IDs**

```csharp
using Quant.Core.Calendars;
using Quant.Core.DayCounters;
using Xunit;

namespace Quant.Core.Tests.Common;

public sealed class CodeCatalogTests
{
    [Theory]
    [InlineData(CalendarCode.BrazilSettlement, 0)]
    [InlineData(CalendarCode.UnitedStatesSettlement, 1)]
    [InlineData(CalendarCode.BrazilUnitedStatesSettlement, 2)]
    public void CalendarIdsAreStable(CalendarCode code, int expected) => Assert.Equal(expected, (int)code);

    [Theory]
    [InlineData(DayCounterCode.Business252, 0)]
    [InlineData(DayCounterCode.Actual365Fixed, 1)]
    [InlineData(DayCounterCode.Thirty360BondBasis, 2)]
    [InlineData(DayCounterCode.Actual360, 3)]
    [InlineData(DayCounterCode.Actual365NoLeap, 4)]
    [InlineData(DayCounterCode.ActualActualIsda, 5)]
    [InlineData(DayCounterCode.ActualActualAfb, 6)]
    [InlineData(DayCounterCode.Thirty360Usa, 7)]
    [InlineData(DayCounterCode.Thirty360European, 8)]
    [InlineData(DayCounterCode.Thirty360Italian, 9)]
    [InlineData(DayCounterCode.Thirty360Nasd, 10)]
    [InlineData(DayCounterCode.OneDay, 11)]
    [InlineData(DayCounterCode.Simple, 12)]
    public void DayCounterIdsAreStable(DayCounterCode code, int expected) => Assert.Equal(expected, (int)code);

    [Fact]
    public void DiscoveryRowsHaveUniqueIdsAndDescriptions()
    {
        Assert.Equal(CodeCatalog.Calendars.Count, CodeCatalog.Calendars.Select(x => x.Id).Distinct().Count());
        Assert.All(CodeCatalog.Calendars.Concat(CodeCatalog.DayCounters), x => Assert.False(string.IsNullOrWhiteSpace(x.Description)));
    }
}
```

- [ ] **Step 2: Run the tests and verify missing-type failures**

Run: `dotnet test tests/Quant.Core.Tests/Quant.Core.Tests.csproj -c Release --filter CodeCatalogTests`

Expected: FAIL because the code types do not exist.

- [ ] **Step 3: Implement explicit enums and metadata**

Use explicit values. The day-counter enum is:

```csharp
namespace Quant.Core.DayCounters;

public enum DayCounterCode
{
    Business252 = 0,
    Actual365Fixed = 1,
    Thirty360BondBasis = 2,
    Actual360 = 3,
    Actual365NoLeap = 4,
    ActualActualIsda = 5,
    ActualActualAfb = 6,
    Thirty360Usa = 7,
    Thirty360European = 8,
    Thirty360Italian = 9,
    Thirty360Nasd = 10,
    OneDay = 11,
    Simple = 12
}
```

Use `0..6` for business-day conventions in the approved order and `0..3` for Months, Years, Weeks, Days. Define metadata without reflection:

```csharp
public readonly record struct CodeDescription(int Id, string Name, string Description);

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
```

- [ ] **Step 4: Run tests and document the immutable public contract**

Run: `dotnet test tests/Quant.Core.Tests/Quant.Core.Tests.csproj -c Release --filter CodeCatalogTests`

Expected: PASS. Copy the complete ID tables and “IDs never change meaning” rule into `docs/functions/codes.md`.

- [ ] **Step 5: Commit codes and metadata**

```powershell
git add -- src/Quant.Core/Common src/Quant.Core/Calendars src/Quant.Core/DayCounters tests/Quant.Core.Tests/Common docs/functions/codes.md
git commit -m "feat: define stable financial convention codes"
```

### Task 2: Build read-only QuantLib catalogs and lifecycle

**Files:**
- Create: `src/Quant.QuantLib/Interop/QuantLibDateConverter.cs`
- Create: `src/Quant.QuantLib/Calendars/HolidayCorrections.cs`
- Create: `src/Quant.QuantLib/Calendars/CalendarCatalog.cs`
- Create: `src/Quant.QuantLib/DayCounters/DayCounterCatalog.cs`
- Create: `src/Quant.QuantLib/Interop/QuantLibConventionMapper.cs`
- Create: `src/Quant.QuantLib/QuantLibRuntime.cs`
- Create: `src/Quant.QuantLib/Properties/AssemblyInfo.cs`
- Create: `tests/Quant.QuantLib.Tests/Calendars/CalendarCatalogTests.cs`
- Create: `tests/Quant.QuantLib.Tests/DayCounters/DayCounterCatalogTests.cs`

**Interfaces:**
- Consumes: all stable code enums from Task 1.
- Produces: internal `CalendarCatalog.Get(CalendarCode)`, `DayCounterCatalog.Get(DayCounterCode, CalendarCode)`, date/convention mapping, and disposable `QuantLibRuntime`.

Grant the integration-test assembly access to internal catalogs:

```csharp
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("Quant.QuantLib.Tests")]
```

- [ ] **Step 1: Write catalog identity and joint-calendar tests**

```csharp
[Fact]
public void CalendarLookupReusesTheSameProxy()
{
    using var catalog = new CalendarCatalog();
    Assert.Same(catalog.Get(CalendarCode.BrazilSettlement), catalog.Get(CalendarCode.BrazilSettlement));
}

[Fact]
public void JointCalendarRequiresBothMarketsToBeOpen()
{
    using var catalog = new CalendarCatalog();
    using var usOnlyHoliday = QuantLibDateConverter.ToQuantLib(new DateOnly(2026, 7, 3));
    Assert.False(catalog.Get(CalendarCode.BrazilUnitedStatesSettlement).isBusinessDay(usOnlyHoliday));
}

[Fact]
public void Business252IsCachedPerCalendar()
{
    using var calendars = new CalendarCatalog();
    using var counters = new DayCounterCatalog(calendars);
    Assert.Same(
        counters.Get(DayCounterCode.Business252, CalendarCode.BrazilSettlement),
        counters.Get(DayCounterCode.Business252, CalendarCode.BrazilSettlement));
}
```

- [ ] **Step 2: Run tests and verify they fail**

Run: `dotnet test tests/Quant.QuantLib.Tests/Quant.QuantLib.Tests.csproj -c Release --filter "CalendarCatalogTests|DayCounterCatalogTests"`

Expected: FAIL because catalogs do not exist.

- [ ] **Step 3: Implement date and convention mapping**

```csharp
internal static QL.Date ToQuantLib(DateOnly value) =>
    new(value.Day, (QL.Month)value.Month, value.Year);

internal static DateOnly FromQuantLib(QL.Date value) =>
    new(value.year(), (int)value.month(), value.dayOfMonth());

internal static QL.BusinessDayConvention ToQuantLib(BusinessDayConventionCode value) => value switch
{
    BusinessDayConventionCode.ModifiedFollowing => QL.BusinessDayConvention.ModifiedFollowing,
    BusinessDayConventionCode.Following => QL.BusinessDayConvention.Following,
    BusinessDayConventionCode.Preceding => QL.BusinessDayConvention.Preceding,
    BusinessDayConventionCode.ModifiedPreceding => QL.BusinessDayConvention.ModifiedPreceding,
    BusinessDayConventionCode.Unadjusted => QL.BusinessDayConvention.Unadjusted,
    BusinessDayConventionCode.HalfMonthModifiedFollowing => QL.BusinessDayConvention.HalfMonthModifiedFollowing,
    BusinessDayConventionCode.Nearest => QL.BusinessDayConvention.Nearest,
    _ => throw new ArgumentOutOfRangeException(nameof(value))
};
```

- [ ] **Step 4: Implement corrected calendars and day counters**

Construct Brazil and US Settlement calendars once. Apply `HolidayCorrections.BrazilAdded` with `addHoliday` and `BrazilRemoved` with `removeHoliday` before constructing the joint calendar. Initialize the first release with explicit empty arrays because no corrections have been supplied yet:

```csharp
internal static class HolidayCorrections
{
    internal static readonly DateOnly[] BrazilAdded = [];
    internal static readonly DateOnly[] BrazilRemoved = [];
    internal static readonly DateOnly[] UnitedStatesAdded = [];
    internal static readonly DateOnly[] UnitedStatesRemoved = [];
}
```

Create the joint calendar with `new QL.JointCalendar(brazil, unitedStates, QL.JointCalendarRule.JoinHolidays)`. Create one instance of every stateless day counter and one `QL.Business252` per calendar. Map all 13 codes with an explicit switch; `Thirty360BondBasis` must use `QL.Thirty360.Convention.BondBasis`.

- [ ] **Step 5: Run catalog and concurrency tests**

Add a test that calls `isBusinessDay` concurrently 10,000 times after initialization, then run:

`dotnet test tests/Quant.QuantLib.Tests/Quant.QuantLib.Tests.csproj -c Release --filter "CalendarCatalogTests|DayCounterCatalogTests"`

Expected: PASS without exceptions or changing results.

- [ ] **Step 6: Commit catalogs and lifecycle**

```powershell
git add -- src/Quant.QuantLib/Interop src/Quant.QuantLib/Calendars src/Quant.QuantLib/DayCounters src/Quant.QuantLib/QuantLibRuntime.cs tests/Quant.QuantLib.Tests/Calendars tests/Quant.QuantLib.Tests/DayCounters
git commit -m "feat: cache QuantLib calendars and day counters"
```

### Task 3: Implement typed calendar services

**Files:**
- Create: `src/Quant.Core/Calendars/ICalendarService.cs`
- Create: `src/Quant.QuantLib/Calendars/QuantLibCalendarService.cs`
- Create: `tests/Quant.QuantLib.Tests/Calendars/QuantLibCalendarServiceTests.cs`
- Modify: `src/Quant.QuantLib/QuantLibRuntime.cs`

**Interfaces:**
- Consumes: `CalendarCatalog` and convention mapper from Task 2.
- Produces: the complete `ICalendarService` contract consumed by Excel UDFs.

- [ ] **Step 1: Define the interface and failing behavior tests**

```csharp
public interface ICalendarService
{
    bool IsBusinessDay(DateOnly date, CalendarCode calendar);
    bool IsHoliday(DateOnly date, CalendarCode calendar);
    DateOnly Adjust(DateOnly date, CalendarCode calendar, BusinessDayConventionCode convention);
    DateOnly AdvanceBusinessDays(DateOnly date, int businessDays, CalendarCode calendar);
    DateOnly AdvanceMonths(DateOnly date, int months, CalendarCode calendar, BusinessDayConventionCode convention);
    DateOnly AdvanceYears(DateOnly date, int years, CalendarCode calendar, BusinessDayConventionCode convention);
    int BusinessDaysBetween(DateOnly startDate, DateOnly endDate, CalendarCode calendar, bool includeStart, bool includeEnd);
    IReadOnlyList<DateOnly> Holidays(DateOnly startDate, DateOnly endDate, CalendarCode calendar, bool includeWeekends);
    DateOnly EndOfMonth(DateOnly date, CalendarCode calendar);
    bool IsEndOfMonth(DateOnly date, CalendarCode calendar);
}
```

Tests must cover a known BR holiday, known US holiday, joint-calendar closure, `includeStart=false/includeEnd=true`, negative business-day advancement, and month/year end-of-month preservation.

- [ ] **Step 2: Run tests and verify missing implementation failures**

Run: `dotnet test tests/Quant.QuantLib.Tests/Quant.QuantLib.Tests.csproj -c Release --filter QuantLibCalendarServiceTests`

Expected: FAIL because `QuantLibCalendarService` does not exist.

- [ ] **Step 3: Implement direct QuantLib delegation**

Reject `endDate < startDate` with `ArgumentOutOfRangeException`. Use `Calendar.adjust`, `Calendar.advance`, `Calendar.businessDaysBetween`, `Calendar.holidayList`, `Calendar.endOfMonth`, and `Calendar.isEndOfMonth`. Month/year calls must pass `endOfMonth: true`; business-day advancement uses `QL.TimeUnit.Days` and does not expose a convention.

```csharp
public DateOnly AdvanceMonths(DateOnly date, int months, CalendarCode calendar, BusinessDayConventionCode convention)
{
    using var input = QuantLibDateConverter.ToQuantLib(date);
    using var output = _calendars.Get(calendar).advance(
        input, months, QL.TimeUnit.Months, QuantLibConventionMapper.ToQuantLib(convention), true);
    return QuantLibDateConverter.FromQuantLib(output);
}
```

- [ ] **Step 4: Run service tests**

Run: `dotnet test tests/Quant.QuantLib.Tests/Quant.QuantLib.Tests.csproj -c Release --filter QuantLibCalendarServiceTests`

Expected: PASS.

- [ ] **Step 5: Commit calendar services**

```powershell
git add -- src/Quant.Core/Calendars/ICalendarService.cs src/Quant.QuantLib/Calendars/QuantLibCalendarService.cs src/Quant.QuantLib/QuantLibRuntime.cs tests/Quant.QuantLib.Tests/Calendars/QuantLibCalendarServiceTests.cs
git commit -m "feat: add QuantLib calendar services"
```

### Task 4: Implement day-count and schedule services

**Files:**
- Create: `src/Quant.Core/DayCounters/IDayCountService.cs`
- Create: `src/Quant.Core/Calendars/IScheduleService.cs`
- Create: `src/Quant.QuantLib/DayCounters/QuantLibDayCountService.cs`
- Create: `src/Quant.QuantLib/Calendars/QuantLibScheduleService.cs`
- Create: `tests/Quant.QuantLib.Tests/DayCounters/QuantLibDayCountServiceTests.cs`
- Create: `tests/Quant.QuantLib.Tests/Calendars/QuantLibScheduleServiceTests.cs`
- Modify: `src/Quant.QuantLib/QuantLibRuntime.cs`

**Interfaces:**
- Consumes: catalogs and converters from Tasks 2-3.
- Produces: `IDayCountService.DayCount`, `IDayCountService.YearFraction`, and `IScheduleService.Generate`.

- [ ] **Step 1: Define typed contracts**

```csharp
public interface IDayCountService
{
    int DayCount(DateOnly startDate, DateOnly endDate, CalendarCode calendar, DayCounterCode dayCounter);
    double YearFraction(DateOnly startDate, DateOnly endDate, CalendarCode calendar, DayCounterCode dayCounter);
}

public interface IScheduleService
{
    IReadOnlyList<DateOnly> Generate(
        DateOnly referenceDate,
        DateOnly maturityDate,
        int interval,
        TimeUnitCode timeUnit,
        CalendarCode calendar,
        BusinessDayConventionCode convention);
}
```

- [ ] **Step 2: Write failing parity and schedule tests**

For every day-counter code, compare service output with a direct call to the corresponding QuantLib counter. Add schedule tests asserting: reference excluded, maturity included, ascending output, six-month spacing when aligned, short front stub when unaligned, and end-of-month preservation.

Run: `dotnet test tests/Quant.QuantLib.Tests/Quant.QuantLib.Tests.csproj -c Release --filter "QuantLibDayCountServiceTests|QuantLibScheduleServiceTests"`

Expected: FAIL because services do not exist.

- [ ] **Step 3: Implement day-count delegation**

Reject `endDate < startDate` with `ArgumentOutOfRangeException`. Convert both dates once per call, retrieve the cached counter, then call only `dayCount` or `yearFraction`:

```csharp
public double YearFraction(DateOnly startDate, DateOnly endDate, CalendarCode calendar, DayCounterCode dayCounter)
{
    using var start = QuantLibDateConverter.ToQuantLib(startDate);
    using var end = QuantLibDateConverter.ToQuantLib(endDate);
    return _dayCounters.Get(dayCounter, calendar).yearFraction(start, end);
}
```

- [ ] **Step 4: Implement backward QuantLib schedules**

Reject `interval <= 0` and `referenceDate >= maturityDate`. Construct a QuantLib `Schedule` with effective/reference date, maturity date, `Period(interval, mappedTimeUnit)`, selected calendar, the same adjustment convention for regular and termination dates, `DateGeneration.Rule.Backward`, and `endOfMonth=true`. Convert its dates, remove the reference date, retain maturity, and return ascending results.

- [ ] **Step 5: Run service tests and commit**

Run: `dotnet test tests/Quant.QuantLib.Tests/Quant.QuantLib.Tests.csproj -c Release --filter "QuantLibDayCountServiceTests|QuantLibScheduleServiceTests"`

Expected: PASS.

```powershell
git add -- src/Quant.Core/DayCounters/IDayCountService.cs src/Quant.Core/Calendars/IScheduleService.cs src/Quant.QuantLib/DayCounters/QuantLibDayCountService.cs src/Quant.QuantLib/Calendars/QuantLibScheduleService.cs src/Quant.QuantLib/QuantLibRuntime.cs tests/Quant.QuantLib.Tests/DayCounters/QuantLibDayCountServiceTests.cs tests/Quant.QuantLib.Tests/Calendars/QuantLibScheduleServiceTests.cs
git commit -m "feat: add QuantLib day-count and schedule services"
```

### Task 5: Add Excel input mapping, errors, and discovery UDFs

**Files:**
- Create: `src/Quant.Excel.AddIn/Conversion/ExcelCodeMapper.cs`
- Create: `src/Quant.Excel.AddIn/Conversion/ExcelDateConverter.cs`
- Create: `src/Quant.Excel.AddIn/Conversion/ExcelTableConverter.cs`
- Create: `src/Quant.Excel.AddIn/Errors/ExcelCall.cs`
- Create: `src/Quant.Excel.AddIn/Functions/FunctionDescriptions.cs`
- Create: `src/Quant.Excel.AddIn/Functions/Information/DiscoveryFunctions.cs`
- Create: `src/Quant.Excel.AddIn/AddInServices.cs`
- Create: `src/Quant.Excel.AddIn/Properties/AssemblyInfo.cs`
- Modify: `src/Quant.Excel.AddIn/AddInLifecycle.cs`
- Create: `tests/Quant.Excel.AddIn.Tests/Conversion/ExcelCodeMapperTests.cs`
- Create: `tests/Quant.Excel.AddIn.Tests/Functions/DiscoveryFunctionsTests.cs`
- Create: `tests/Quant.Excel.AddIn.Tests/Functions/DescriptionTests.cs`

**Interfaces:**
- Consumes: `CodeCatalog` and `QuantLibRuntime`.
- Produces: direct integer-to-enum switches, Excel error mapping, service composition, and four discovery UDFs.

Grant the Excel test assembly access to internal boundary helpers:

```csharp
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("Quant.Excel.AddIn.Tests")]
```

- [ ] **Step 1: Write failing code/error and metadata tests**

Tests must assert every valid integer maps correctly, invalid codes produce `ExcelError.ExcelErrorValue`, each discovery function returns ID/name/description columns, every exported method has a nonempty `ExcelFunction.Description`, and every parameter has a nonempty `ExcelArgument.Description`.

- [ ] **Step 2: Run tests and verify failures**

Run: `dotnet test tests/Quant.Excel.AddIn.Tests/Quant.Excel.AddIn.Tests.csproj -c Release --filter "ExcelCodeMapperTests|DiscoveryFunctionsTests|DescriptionTests"`

Expected: FAIL because boundary helpers and UDFs do not exist.

- [ ] **Step 3: Implement explicit code switches and Excel errors**

```csharp
internal static CalendarCode Calendar(int value) => value switch
{
    0 => CalendarCode.BrazilSettlement,
    1 => CalendarCode.UnitedStatesSettlement,
    2 => CalendarCode.BrazilUnitedStatesSettlement,
    _ => throw new ArgumentException("Unsupported calendar code.", nameof(value))
};

internal static object Execute(Func<object> calculation)
{
    try { return calculation(); }
    catch (ArgumentOutOfRangeException) { return ExcelError.ExcelErrorNum; }
    catch (ArgumentException) { return ExcelError.ExcelErrorValue; }
    catch { return ExcelError.ExcelErrorValue; }
}
```

Do not use `Enum.Parse`, `Enum.IsDefined`, reflection, or per-call dictionaries.

- [ ] **Step 4: Register discovery UDFs with complete help**

```csharp
[ExcelFunction(Name = "bCalendars", Description = FunctionDescriptions.Calendars, IsThreadSafe = true)]
public static object[,] Calendars() => ExcelTableConverter.From(CodeCatalog.Calendars);
```

Implement equally explicit `bDayCounters`, `bBusinessDayConventions`, and `bTimeUnits`. `AddInLifecycle.AutoOpen` creates one `QuantLibRuntime`; `AutoClose` disposes it. Tests call the same composition root and reset it after each test assembly.

- [ ] **Step 5: Run tests and commit**

Run: `dotnet test tests/Quant.Excel.AddIn.Tests/Quant.Excel.AddIn.Tests.csproj -c Release --filter "ExcelCodeMapperTests|DiscoveryFunctionsTests|DescriptionTests"`

Expected: PASS.

```powershell
git add -- src/Quant.Excel.AddIn/Conversion src/Quant.Excel.AddIn/Errors src/Quant.Excel.AddIn/Functions src/Quant.Excel.AddIn/AddInServices.cs src/Quant.Excel.AddIn/AddInLifecycle.cs tests/Quant.Excel.AddIn.Tests
git commit -m "feat: add convention discovery UDFs"
```

### Task 6: Export calendar UDFs

**Files:**
- Create: `src/Quant.Excel.AddIn/Functions/Calendars/CalendarFunctions.cs`
- Create: `tests/Quant.Excel.AddIn.Tests/Functions/CalendarFunctionsTests.cs`
- Create: `docs/functions/calendars.md`

**Interfaces:**
- Consumes: `ICalendarService`, `ExcelCodeMapper`, `ExcelCall`, and `FunctionDescriptions`.
- Produces: all approved `b...` calendar/date functions.

- [ ] **Step 1: Write one failing test per exported function**

Cover `bIsBusinessDay`, `bIsHoliday`, `bAdjustDate`, `bAdvanceDays`, `bAdvanceMonths`, `bAdvanceYears`, `bBDays`, `bHolidays`, `bEndOfMonth`, and `bIsEndOfMonth`. Assert Modified Following default, `bBDays` defaults, x64-safe date conversion, invalid code `#VALUE!`, invalid range `#NUM!`, and vertical spill shape for holidays.

- [ ] **Step 2: Run tests and verify missing-UDF failures**

Run: `dotnet test tests/Quant.Excel.AddIn.Tests/Quant.Excel.AddIn.Tests.csproj -c Release --filter CalendarFunctionsTests`

Expected: FAIL because `CalendarFunctions` does not exist.

- [ ] **Step 3: Implement thin functions with Excel-only `b` names**

Representative function:

```csharp
[ExcelFunction(
    Name = "bBDays",
    Description = "Returns the QuantLib business-day count between startDate and endDate for calendarCode. By default startDate is excluded and endDate is included.",
    IsThreadSafe = true)]
public static object BusinessDays(
    [ExcelArgument(Name = "startDate", Description = "First Excel date in the interval.")] DateTime startDate,
    [ExcelArgument(Name = "endDate", Description = "Last Excel date in the interval.")] DateTime endDate,
    [ExcelArgument(Name = "calendarCode", Description = "Calendar ID returned by bCalendars().")] int calendarCode,
    [ExcelArgument(Name = "includeStart", Description = "Optional; include startDate. Default FALSE.")] bool includeStart = false,
    [ExcelArgument(Name = "includeEnd", Description = "Optional; include endDate. Default TRUE.")] bool includeEnd = true) =>
    ExcelCall.Execute(() => AddInServices.Calendar.BusinessDaysBetween(
        ExcelDateConverter.ToDateOnly(startDate),
        ExcelDateConverter.ToDateOnly(endDate),
        ExcelCodeMapper.Calendar(calendarCode),
        includeStart,
        includeEnd));
```

All C# method names omit the `b` prefix. Month/year functions omit any end-of-month argument because preservation is always enabled in the service.

- [ ] **Step 4: Run tests and write function documentation**

Run: `dotnet test tests/Quant.Excel.AddIn.Tests/Quant.Excel.AddIn.Tests.csproj -c Release --filter CalendarFunctionsTests`

Expected: PASS. `docs/functions/calendars.md` must include signatures, defaults, code links, examples, inclusion semantics, EOM behavior, and joint-calendar behavior.

- [ ] **Step 5: Commit calendar UDFs**

```powershell
git add -- src/Quant.Excel.AddIn/Functions/Calendars tests/Quant.Excel.AddIn.Tests/Functions/CalendarFunctionsTests.cs docs/functions/calendars.md
git commit -m "feat: expose calendar UDFs"
```

### Task 7: Export day-count and schedule UDFs

**Files:**
- Create: `src/Quant.Excel.AddIn/Functions/DayCounters/DayCounterFunctions.cs`
- Create: `src/Quant.Excel.AddIn/Functions/Calendars/ScheduleFunctions.cs`
- Create: `tests/Quant.Excel.AddIn.Tests/Functions/DayCounterFunctionsTests.cs`
- Create: `tests/Quant.Excel.AddIn.Tests/Functions/ScheduleFunctionsTests.cs`
- Create: `docs/functions/day-counters.md`
- Create: `docs/functions/schedules.md`

**Interfaces:**
- Consumes: `IDayCountService`, `IScheduleService`, and Excel boundary helpers.
- Produces: `bDayCount`, `bYearFraction`, and `bSchedule`.

- [ ] **Step 1: Write failing UDF tests**

Test every supported day-counter code through both UDFs against direct service output. Test invalid calendar/day-counter/time-unit IDs, nonpositive schedule interval, reversed schedule dates, six-month aligned schedules, short front stubs, EOM maturity, reference exclusion, maturity inclusion, chronological order, and vertical array shape.

- [ ] **Step 2: Run tests and verify missing-UDF failures**

Run: `dotnet test tests/Quant.Excel.AddIn.Tests/Quant.Excel.AddIn.Tests.csproj -c Release --filter "DayCounterFunctionsTests|ScheduleFunctionsTests"`

Expected: FAIL because the functions do not exist.

- [ ] **Step 3: Implement the three thin UDFs**

Use these exact Excel signatures:

```text
bDayCount(startDate, endDate, calendarCode, dayCounterCode)
bYearFraction(startDate, endDate, calendarCode, dayCounterCode)
bSchedule(referenceDate, maturityDate, interval, timeUnit, calendarCode, [businessDayConvention=0])
```

`bSchedule` converts the service list into a one-column `object[,]` of `DateTime` values. All parameters receive explicit Excel help descriptions, including the short-front-stub and EOM rules.

- [ ] **Step 4: Run tests and update user documentation**

Run: `dotnet test tests/Quant.Excel.AddIn.Tests/Quant.Excel.AddIn.Tests.csproj -c Release --filter "DayCounterFunctionsTests|ScheduleFunctionsTests|DescriptionTests"`

Expected: PASS. Document examples for Business/252, Actual/365 Fixed, 30/360 Bond Basis, and a six-month backward schedule.

- [ ] **Step 5: Commit day-count and schedule UDFs**

```powershell
git add -- src/Quant.Excel.AddIn/Functions/DayCounters src/Quant.Excel.AddIn/Functions/Calendars/ScheduleFunctions.cs tests/Quant.Excel.AddIn.Tests/Functions/DayCounterFunctionsTests.cs tests/Quant.Excel.AddIn.Tests/Functions/ScheduleFunctionsTests.cs docs/functions/day-counters.md docs/functions/schedules.md
git commit -m "feat: expose day-count and schedule UDFs"
```

### Task 8: Verify the complete first calculation release

**Files:**
- Create: `docs/functions/quick-start.md`
- Modify: `README.md`
- Modify: `eng/verify-package.ps1`
- Create: `docs/performance/calendar-daycount.md`

**Interfaces:**
- Consumes: all prior tasks.
- Produces: a release verification command and user-facing entry point.

- [ ] **Step 1: Add release verification assertions**

Extend `eng/verify-package.ps1` to run the full Release test suite, build the XLL, and inspect the add-in assembly for the 17 approved Excel registrations: four discovery, ten calendar/date, two day-count, and one schedule function.

- [ ] **Step 2: Run the full verification**

Run:

```powershell
.\eng\build-native.ps1
dotnet test Quant.sln -c Release
.\eng\verify-package.ps1
```

Expected: all tests PASS, exactly the approved public function names are registered, only an x64 XLL is produced, and `NQuantLibc.dll` is packaged.

- [ ] **Step 3: Complete quick-start documentation**

Show loading, code discovery, `bBDays`, `bYearFraction`, and `bSchedule` examples. Link every function-reference page and the native-build guide from `README.md`. In `docs/performance/calendar-daycount.md`, document cached SWIG objects, explicit switches, thread-safety assumptions, native-call costs, and why managed reimplementations were rejected.

- [ ] **Step 4: Commit verification and documentation**

```powershell
git add -- eng/verify-package.ps1 docs/functions/quick-start.md docs/performance/calendar-daycount.md README.md
git commit -m "docs: complete calendar and day-count release guide"
```

## Plan Verification

Run the commands in Task 8. In Excel x64, manually call `bCalendars()`, `bDayCounters()`, `bBDays(...)`, `bYearFraction(...)`, and `bSchedule(...)`; confirm descriptions appear in the Function Arguments dialog and spilled arrays have the documented shape.

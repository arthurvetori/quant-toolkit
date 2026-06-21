# Day-count functions

Day-counter and calendar IDs are listed in [financial convention codes](codes.md) and can also be discovered in Excel with `bDayCounters()` and `bCalendars()`.

## Functions

```text
bDayCount(startDate, endDate, calendarCode, dayCounterCode)
bYearFraction(startDate, endDate, calendarCode, dayCounterCode)
```

`bDayCount` returns the convention's integer numerator. `bYearFraction` returns its year fraction. Dates must be chronological; a reversed range returns `#NUM!`. Invalid IDs return `#VALUE!`.

The calendar matters directly for Business/252 and is accepted consistently for every counter. Calendar `0` is Brazil Settlement.

## Examples

```excel
=bDayCount(DATE(2026,1,2),DATE(2026,2,2),0,0)
=bYearFraction(DATE(2025,1,1),DATE(2026,1,1),0,1)
=bYearFraction(DATE(2025,1,31),DATE(2025,7,31),0,2)
```

These apply Business/252 (`0`), Actual/365 Fixed (`1`), and 30/360 Bond Basis (`2`) respectively. The second example returns `1`; the third returns `0.5`.

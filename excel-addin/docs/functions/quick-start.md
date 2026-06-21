# Calendar and day-count quick start

Build and verify the packed x64 XLL, then follow [loading the add-in](loading-the-add-in.md). The native prerequisites and reproducible build steps are in the [Windows x64 native build guide](../native-build/windows-x64.md).

## Discover the stable codes

Enter these formulas in empty cells. Each spills an ID, name, and description table:

```excel
=bCalendars()
=bBusinessDayConventions()
=bTimeUnits()
=bDayCounters()
```

The same values are listed in [financial convention codes](codes.md). Use the returned integer IDs in calculation functions.

## Count Brazil business days

```excel
=bBDays(DATE(2026,6,19),DATE(2026,6,22),0)
```

Calendar `0` is Brazil Settlement. The default interval excludes the start and includes the end, so the result is `1`.

## Calculate a year fraction

```excel
=bYearFraction(DATE(2025,1,1),DATE(2026,1,1),0,1)
```

Day counter `1` is Actual/365 Fixed; the result is `1`. See [day-count functions](day-counters.md) for other conventions.

## Generate a schedule

```excel
=bSchedule(DATE(2025,2,1),DATE(2026,1,15),6,0,0,4)
```

This requests six-month periods with unadjusted dates. The backward schedule spills `15-Jul-2025` and `15-Jan-2026` vertically, with a short front stub. See [schedule function](schedules.md) for its date rules.

The complete calendar/date function reference is in [calendar functions](calendars.md).

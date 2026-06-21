# Schedule function

## Function

```text
bSchedule(referenceDate, maturityDate, interval, timeUnit, calendarCode, [businessDayConvention=0])
```

`bSchedule` generates dates backward from maturity and spills them chronologically in one column. It excludes `referenceDate`, includes `maturityDate`, preserves end-of-month relationships, and creates a short front stub when the dates are not tenor-aligned.

`interval` must be positive and maturity must follow the reference date; otherwise the function returns `#NUM!`. Invalid time-unit, calendar, or convention IDs return `#VALUE!`. The default convention is `0`, Modified Following. See [financial convention codes](codes.md) for all IDs.

## Six-month example

```excel
=bSchedule(DATE(2025,2,1),DATE(2026,1,15),6,0,0,4)
```

Time unit `0` means months and convention `4` means Unadjusted. The backward schedule has a short front stub and spills:

```text
15-Jul-2025
15-Jan-2026
```

For an end-of-month reference and maturity, the generated dates retain the end-of-month relationship before the selected business-day adjustment is applied.

# Calendar functions

Calendar IDs and business-day convention IDs are listed in [financial convention codes](codes.md). Calendar `2` is a `JoinHolidays` calendar: a date is a business day only when both the Brazil and United States settlement markets are open.

## Functions

```text
bIsBusinessDay(date, calendarCode)
bIsHoliday(date, calendarCode)
bAdjustDate(date, calendarCode, [businessDayConvention=0])
bAdvanceDays(date, businessDays, calendarCode)
bAdvanceMonths(date, months, calendarCode, [businessDayConvention=0])
bAdvanceYears(date, years, calendarCode, [businessDayConvention=0])
bBDays(startDate, endDate, calendarCode, [includeStart=FALSE], [includeEnd=TRUE])
bHolidays(startDate, endDate, calendarCode, [includeWeekends=FALSE])
bEndOfMonth(date, calendarCode)
bIsEndOfMonth(date, calendarCode)
```

Convention `0`, Modified Following, is the default wherever a convention is optional. Invalid IDs return `#VALUE!`; reversed date ranges return `#NUM!`.

## Date adjustment and advancement

`bAdjustDate` applies the selected QuantLib convention. `bAdvanceDays` advances by business days and accepts negative values. Month and year advancement always enables QuantLib end-of-month preservation; there is no worksheet argument to disable it.

Examples:

```excel
=bAdjustDate(DATE(2026,1,31),0)
=bAdvanceDays(DATE(2026,6,22),-1,0)
=bAdvanceMonths(DATE(2026,1,30),1,0)
```

For the Brazil settlement calendar, these return 30-Jan-2026, 19-Jun-2026, and 27-Feb-2026 respectively.

## Business-day inclusion

`bBDays` excludes `startDate` and includes `endDate` by default. Set the two optional Boolean arguments explicitly when a different interval convention is required.

```excel
=bBDays(DATE(2026,6,19),DATE(2026,6,22),0)
```

The result is `1`: Friday is excluded and Monday is included.

## Holidays and month end

`bHolidays` spills one Excel date per row. Weekends are excluded unless `includeWeekends` is `TRUE`. The range is inclusive and must be chronological.

`bEndOfMonth` returns the last business day of the month. `bIsEndOfMonth` tests the same QuantLib relationship, so a Friday can be month end when the calendar month ends during a weekend.

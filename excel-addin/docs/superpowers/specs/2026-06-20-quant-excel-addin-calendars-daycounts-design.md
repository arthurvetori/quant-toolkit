# Quant Excel Add-in: Calendars and Day Counters Design

**Date:** 2026-06-20
**Status:** Approved design
**Initial platform:** 64-bit Excel on Windows
**Calculation library:** Official QuantLib C++ with official QuantLib-SWIG C# bindings

## 1. Purpose

Build the first release of a high-performance Excel-DNA add-in that exposes QuantLib calendar, date, schedule, and day-count calculations to Excel. This release establishes boundaries and conventions that later modules will reuse for yield curves, volatility, bonds, options, and optimization.

The initial API is stateless. It does not expose handles. A later release may add overloads that accept calendar or financial-object handles without breaking the integer-code API defined here.

## 2. Design Principles

1. QuantLib is the sole calculation authority. C# must not reimplement business-day, 30/360, year-fraction, or schedule algorithms.
2. Successful calculations must avoid repeated native-object construction, reflection, string parsing, file I/O, and logging.
3. Public Excel names begin with `b`; C# methods and types do not.
4. Public integer codes are explicit, stable API values. They must never depend on C# or QuantLib enum ordinals.
5. Worksheet calculation functions are type-stable: they return the documented value type or a native Excel error.
6. Every exported function and core operation requires unit tests.
7. Documentation is part of the definition of done and must be updated with every affected feature.

## 3. Scope

### Included

- Brazil Settlement calendar.
- United States Settlement calendar.
- Brazil plus United States joint settlement calendar using `JoinHolidays`; a date is a business day only when both markets are open.
- Holiday corrections maintained directly in code.
- Calendar inspection and date adjustment functions.
- Business-day counting and holiday listing.
- QuantLib day counters that can be used correctly from the initial API.
- Backward-generated payment schedules.
- Code-discovery functions for calendars, day counters, business-day conventions, and time units.
- Optional asynchronous diagnostics controlled by Excel commands.
- Official QuantLib and QuantLib-SWIG source builds for x64.

### Deferred

- Calendar, curve, volatility-surface, or instrument handles.
- Actual/Actual ISMA and other conventions that require schedules or reference periods not present in the simple API.
- Canadian Actual/365 and conventions requiring additional reference dates.
- Runtime calendar mutation.
- Yield curves, volatility, instruments, pricing, and optimization.
- Performance targets tied to a specific workbook size. The design will remain performance-oriented, while workload-specific tuning is deferred until representative workbooks exist.

## 4. Project Structure

All production C# code lives under `src/`.

```text
src/
|-- Quant.Excel.AddIn/
|   |-- Commands/
|   |-- Conversion/
|   |-- Errors/
|   |-- Functions/
|   |   |-- Calendars/
|   |   |-- DayCounters/
|   |   `-- Information/
|   `-- AddInLifecycle.cs
|-- Quant.Core/
|   |-- Calendars/
|   |-- Common/
|   |-- DayCounters/
|   `-- Diagnostics/
|-- Quant.QuantLib/
|   |-- Calendars/
|   |-- DayCounters/
|   `-- Interop/
`-- Quant.Infrastructure/
    `-- Diagnostics/

tests/
|-- Quant.Core.Tests/
|-- Quant.QuantLib.Tests/
`-- Quant.Excel.AddIn.Tests/

docs/
|-- architecture/
|-- decisions/
|-- functions/
|-- native-build/
`-- performance/
```

### Project responsibilities

- `Quant.Excel.AddIn` is a thin Excel-DNA facade. It owns Excel function registration, argument descriptions, optional-argument normalization, Excel date and array conversion, and native Excel error translation.
- `Quant.Core` owns stable public codes, interfaces, validation rules, and orchestration. It has no Excel dependency.
- `Quant.QuantLib` owns the official SWIG integration, QuantLib object catalogs, holiday corrections, and calculations.
- `Quant.Infrastructure` owns operational services such as asynchronous diagnostics. It does not contain financial logic.

This layered boundary adds files and interfaces compared with direct wrappers, but it prevents Excel, infrastructure, and QuantLib concerns from becoming entangled as the library grows.

## 5. Patterns and Their Rationale

### Thin facade

Excel UDF methods only translate inputs and outputs before delegating. This keeps Excel marshaling out of calculation services and makes core behavior testable without Excel.

### Static catalog

Calendars and day counters are constructed once, corrected before publication, and reused for the add-in session. This removes repeated native allocations and provides one authoritative mapping from public codes to QuantLib objects.

### Explicit factory switches

Small integer catalogs use explicit `switch` mappings instead of reflection, `Enum.Parse`, or a dependency-injection container. This provides fast, predictable dispatch while keeping every supported mapping visible in code.

### Immutable-after-initialization lifecycle

Calendar corrections are applied before shared instances become available. Calculations perform read-only operations thereafter. This avoids mutation races and makes Excel-DNA multi-threaded execution safe for the supported operations.

### Adapter boundary

Excel dates, arrays, missing values, and errors are converted at the add-in boundary. QuantLib-specific types remain inside `Quant.QuantLib`, allowing either side to change without leaking implementation details.

## 6. Native Dependency Strategy

The project builds pinned revisions of official QuantLib and QuantLib-SWIG for C# and x64 Windows. The add-in repository records the exact compatible revisions and documents a reproducible native build.

QuantLib should remain close to upstream rather than becoming a permanent broad fork. Permanent market-calendar corrections may be maintained in the native calendar source when appropriate. Add-in-specific corrections may be applied during calendar initialization, but never during worksheet calculation. The resulting native and managed assemblies are packaged with the x64 add-in.

## 7. Stable Public Codes

Public codes are defined with explicit integer values. Tests must prevent renumbering, duplication, or accidental reuse.

### Calendars

| ID | Meaning |
|---:|---|
| 0 | Brazil Settlement |
| 1 | United States Settlement |
| 2 | Brazil + United States Settlement (`JoinHolidays`) |

### Day counters

| ID | Meaning |
|---:|---|
| 0 | Business/252 |
| 1 | Actual/365 Fixed |
| 2 | 30/360 Bond Basis |
| 3 | Actual/360 |
| 4 | Actual/365 No Leap |
| 5 | Actual/Actual ISDA |
| 6 | Actual/Actual AFB |
| 7 | 30/360 USA |
| 8 | 30/360 European |
| 9 | 30/360 Italian |
| 10 | 30/360 NASD |
| 11 | One Day |
| 12 | Simple |

### Business-day conventions

| ID | Meaning |
|---:|---|
| 0 | Modified Following |
| 1 | Following |
| 2 | Preceding |
| 3 | Modified Preceding |
| 4 | Unadjusted |
| 5 | Half-Month Modified Following |
| 6 | Nearest |

Modified Following is the default wherever a business-day convention is optional.

### Time units

| ID | Meaning |
|---:|---|
| 0 | Months |
| 1 | Years |
| 2 | Weeks |
| 3 | Days |

## 8. Excel API

The `b` prefix is present only in the Excel-DNA registration name. C# methods use conventional names such as `DayCount` and `YearFraction`.

### Discovery

```text
bCalendars()
bDayCounters()
bBusinessDayConventions()
bTimeUnits()
```

Each function spills a table containing ID, short name, and full description.

### Calendar and date functions

```text
bIsBusinessDay(date, calendarCode)
bIsHoliday(date, calendarCode)
bAdjustDate(date, calendarCode, [businessDayConvention=0])

bAdvanceDays(date, businessDays, calendarCode)
bAdvanceMonths(date, months, calendarCode,
               [businessDayConvention=0])
bAdvanceYears(date, years, calendarCode,
              [businessDayConvention=0])

bBDays(startDate, endDate, calendarCode,
       [includeStart=false], [includeEnd=true])

bHolidays(startDate, endDate, calendarCode,
          [includeWeekends=false])

bEndOfMonth(date, calendarCode)
bIsEndOfMonth(date, calendarCode)
```

`bAdvanceDays` advances business days using the selected QuantLib calendar. Month and year advancement always preserve an end-of-month relationship when the input date is at the end of its month.

### Day-count functions

```text
bDayCount(startDate, endDate, calendarCode, dayCounterCode)
bYearFraction(startDate, endDate, calendarCode, dayCounterCode)
```

`bDayCount` returns QuantLib's raw integer day count. `bYearFraction` returns QuantLib's convention-adjusted fraction. The selected calendar is used by calendar-aware counters such as Business/252. Reference-period arguments are not exposed in this release.

### Schedule function

```text
bSchedule(referenceDate, maturityDate, interval, timeUnit,
          calendarCode, [businessDayConvention=0])
```

The schedule is generated by QuantLib using a backward rule anchored on `maturityDate`. It:

- uses a positive interval;
- always preserves end-of-month relationships;
- produces a short front stub when the interval does not align with `referenceDate`;
- excludes `referenceDate`;
- includes `maturityDate`; and
- spills payment dates in chronological order.

## 9. Data Flow and Runtime Behavior

```text
Excel input
  -> thin Excel-DNA function
  -> input conversion and validation
  -> explicit integer-code mapping
  -> QuantLib service using a shared read-only object
  -> Excel-compatible result or native Excel error
```

The hot path must not use reflection, general-purpose service lookup, string-based enum parsing, file I/O, or synchronous logging. Calendar-specific Business/252 instances are prebuilt. Stateless day-counter instances are reused. Excel-DNA functions are registered as thread-safe only when the full called path is read-only and safe for concurrent use.

SWIG objects live for the add-in session. The add-in lifecycle owns controlled initialization and shutdown so objects are not disposed while Excel calculations can still use them.

## 10. Error Handling

Basic calculations return the documented result or a native Excel error:

- `#VALUE!` for invalid input types or unsupported integer codes.
- `#NUM!` for invalid date ranges, nonpositive intervals, or dates outside supported QuantLib bounds.

No calculation function returns a descriptive string in place of a number, date, Boolean, or array. This prevents Excel aggregations from silently ignoring failures.

Future complex builders and handlers, such as curves and volatility surfaces, may receive dedicated validation or diagnostic functions. Basic calendar and day-count calculations do not require companion validators because discovery functions and Excel help document their small input surfaces.

Unexpected exceptions are contained at the Excel boundary. When diagnostics are enabled, their details are submitted to the asynchronous diagnostics service without blocking Excel.

## 11. Optional Asynchronous Diagnostics

Logging is disabled by default and never records successful calculations automatically.

```text
bLoggingStart    Excel command/macro
bLoggingStop     Excel command/macro
bLoggingStatus() worksheet function
```

Start and stop are commands rather than worksheet functions because worksheet recalculation may repeat, reorder, or omit side-effecting calls.

When enabled:

- errors and explicit diagnostic events use a non-blocking bounded queue;
- a single background worker writes batches;
- repeated equivalent errors are rate-limited;
- a full queue drops messages instead of blocking Excel; and
- add-in shutdown performs a time-bounded flush.

Asynchronous logging cannot have literally zero cost. With successful-call logging disabled, it adds no queue or file operations to the normal calculation path. Error paths pay only the bounded enqueue cost when diagnostics are enabled.

## 12. Testing Strategy

Every exported UDF and every public core operation requires tests before implementation is considered complete.

### Unit tests

- Success behavior for every function.
- Optional-argument defaults.
- Invalid types and unsupported codes.
- Date boundaries and reversed ranges.
- Inclusion behavior for `bBDays`.
- End-of-month preservation.
- Schedule alignment and short-front-stub behavior.
- Discovery-table contents.
- Nonempty Excel function and argument descriptions.
- Exact public enum values, uniqueness, and stability.

### QuantLib integration tests

- Wrapper results compared with direct calls to the same official QuantLib build.
- Brazil, United States, and joint-calendar fixtures.
- Known holidays, corrected holidays, weekends, and dates when only one joint market is open.
- All supported day-counter variants, including Business/252 and 30/360 Bond Basis.
- Concurrent read-only calls through reused SWIG objects.

### Diagnostics tests

- Disabled logging performs no writes.
- Start and stop are idempotent.
- Queue overflow does not block callers.
- Duplicate rate limiting works.
- Shutdown respects its flush timeout.

Workload-specific benchmarks may be added when representative real-time workbooks are available. Performance-sensitive implementation choices must still be explained and reviewed from the first release.

## 13. Documentation Policy

Documentation is a required deliverable for every change. The repository will maintain:

- a README that links build, usage, and architecture material;
- reproducible QuantLib and QuantLib-SWIG x64 build instructions;
- architecture and project-boundary documentation;
- a stable public-code reference;
- a complete Excel function reference with examples;
- diagnostics and troubleshooting guidance;
- performance decisions and trade-offs; and
- architecture decision records for consequential patterns or dependency choices.

Every Excel function must have a complete `ExcelFunction` description and a description for every `ExcelArgument`. Descriptions state accepted codes, optional defaults, inclusion rules, spill behavior, and important convention semantics. Public C# APIs also receive XML documentation.

Feature work is complete only when code, tests, Excel help metadata, and affected Markdown documentation agree.

## 14. Future Compatibility

Later releases may add handle-aware overloads and modules for curves, volatility, instruments, pricing, and optimization. Those modules will reuse the facade, adapter, catalog, explicit-code, error, diagnostics, testing, and documentation conventions established here.

The current integer-code functions remain supported when handles are introduced. New enum entries may be appended with new explicit values, but existing IDs must never change meaning.

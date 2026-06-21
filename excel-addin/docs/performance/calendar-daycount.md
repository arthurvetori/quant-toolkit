# Calendar and day-count performance

## Cached native objects

The add-in creates one QuantLib runtime when Excel loads it. That runtime caches the three SWIG calendar objects and all day-counter objects, including one Business/252 counter per calendar. Holiday corrections are applied once during initialization. Worksheet calls reuse these objects and dispose only their short-lived date, period, schedule, and result wrappers.

## Stable explicit mappings

Every worksheet integer passes through an explicit `switch` before reaching QuantLib. The add-in does not cast public IDs to enum ordinals. This adds a small validation step, keeps released IDs stable as internal enums evolve, and maps unsupported IDs to `#VALUE!` at the Excel boundary.

## Thread-safety model

The functions are registered as thread-safe so Excel may calculate independent cells concurrently. Shared calendars and day counters are fully constructed before publication and are read-only during worksheet evaluation; holiday mutation occurs only at startup. Each call owns its temporary SWIG wrappers. This design assumes QuantLib's concurrent read-only operations on these cached objects do not mutate shared evaluation state.

## Native-call costs

Scalar calendar and day-count functions make a small number of managed-to-native SWIG calls, so interop overhead can dominate very small calculations. `bHolidays` and `bSchedule` also allocate a managed two-dimensional array proportional to the spill size. Prefer one spilled range over many formulas that regenerate the same list, and avoid volatile wrappers around these functions.

## Why the rules are not reimplemented in managed code

Managed replicas might avoid some boundary calls, but they would create a second source of truth for holiday logic, business-day conventions, end-of-month behavior, stubs, and day-count edge cases. The release deliberately delegates those semantics to the official QuantLib 1.42.1 implementation. Cached SWIG objects reduce setup cost while preserving parity with the native library.

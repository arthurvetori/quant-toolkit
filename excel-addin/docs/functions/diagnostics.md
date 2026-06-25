# Diagnostics functions

Diagnostics are opt-in and disabled by default. Successful calculations never produce a diagnostic event, whether logging is on or off. Only an unexpected, non-input-validation exception inside a UDF generates one event. `#NUM!`/`#VALUE!` results from invalid arguments (for example a reversed date range or an unrecognized calendar code) are not logged; they never reach the diagnostics path.

## Functions

```text
bLoggingStart()
bLoggingStop()
bLoggingStatus()
```

`bLoggingStart` and `bLoggingStop` are Excel commands (run them from the macro dialog, a button, or a keyboard shortcut — not as worksheet formulas). `bLoggingStatus()` is a thread-safe worksheet function that can be entered in a cell.

## Starting and stopping

Run `bLoggingStart` once per Excel session to enable diagnostics. It is idempotent: calling it again while already enabled is a no-op. Diagnostics write to:

```text
%LOCALAPPDATA%\Quant\Logs
```

One log file per Excel process is created there, named `quant-<yyyyMMdd>-<processId>.log`.

Run `bLoggingStop` to disable diagnostics and perform a bounded flush (up to 2 seconds) of any buffered events before returning. It is also idempotent. The add-in additionally stops diagnostics automatically when Excel unloads it, so logging does not need to be stopped manually before closing Excel.

## Checking status

```excel
=bLoggingStatus()
```

Returns `"Disabled"` when diagnostics have never been started (or have been stopped), or `"Enabled"` while a logging session is active. This function only reads the current state — calling it never starts or stops logging.

## What gets logged

Each log line contains a UTC timestamp, the failing UDF's name, the exception type and message, and (if present) the stack trace. Diagnostic events never include worksheet values, cell references, or function arguments — only the function name and exception details are recorded. This holds even while diagnostics are enabled and even when the triggering call's inputs would otherwise be sensitive.

## Behavior under load

The event queue has a fixed capacity. If events arrive faster than they can be written (for example a burst of failures across many cells), additional events are dropped rather than blocking the calculation thread; worksheet recalculation is never slowed down by diagnostics. Repeated occurrences of the same failure (same UDF name and same exception message) within a short duplicate-suppression window are also collapsed into a single log entry, so the log does not fill with copies of one recurring error.

## Examples

```excel
=bLoggingStatus()
```

Run `bLoggingStart` from the macro dialog before evaluating formulas you want diagnosed, then `bLoggingStop` afterward. Inspect the log file under `%LOCALAPPDATA%\Quant\Logs` for any unexpected-error entries; expected validation errors (`#NUM!`/`#VALUE!`) will not appear there.

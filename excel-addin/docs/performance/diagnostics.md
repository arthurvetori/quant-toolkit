# Diagnostics performance

## Zero cost on the success path

When diagnostics are disabled (the default), the active sink is `NullDiagnosticSink`, whose `TryWrite` always returns `false` without doing any work and whose `Status` is a static, precomputed `Disabled` value. A successful UDF call never touches the diagnostics sink at all — there is no check, allocation, or branch on the calculation's success path attributable to diagnostics. Cost is therefore truly zero when nothing has gone wrong, both with diagnostics enabled and disabled.

## Asynchronous logging minimizes, but cannot eliminate, error-path overhead

When diagnostics are enabled, an unexpected exception inside a UDF call still incurs the cost of constructing one `DiagnosticEvent` (function name, exception type/message, stack trace) and a non-blocking attempt to enqueue it. This is deliberately minimized — the write never awaits and never blocks on I/O — but it is not zero: there is a duplicate-suppression dictionary lookup, a bounded-channel write attempt, and (on the failure path of either of those) an `Interlocked` increment. This overhead is paid only on the already-exceptional, already-slow error path (an unhandled exception was already thrown and caught), so it does not affect the cost of normal calculations, but it should not be described as zero. The trade-off is: pay a small, bounded amount of extra work on a path that is already an order of magnitude more expensive than a normal calculation (exception throw/catch), in exchange for non-blocking, durable error visibility.

## Bounded, non-blocking producer

`AsyncFileDiagnosticSink.TryWrite` enqueues onto a `System.Threading.Channels` bounded channel (default capacity 1024, configurable) opened with `FullMode.Wait`. Critically, producers call the synchronous `Writer.TryWrite`, not an awaited write — so even though the channel's full-mode is configured to `Wait`, the calling UDF thread never awaits or blocks on a full queue. When the channel is full, `TryWrite` returns `false` immediately, the event is dropped, and a dropped-event counter (`Interlocked`-incremented) is updated. This guarantees the worksheet calculation thread is never stalled by the diagnostics subsystem, even under a burst of thousands of failures.

A duplicate-suppression window collapses repeated occurrences of the same `(Source, Message)` pair so that one recurring error does not flood the queue (and the log) with copies. A dropped (queue-full) write is never recorded in the duplicate index, so a later genuinely new occurrence of the same key is not incorrectly suppressed as a duplicate of an event that was never actually written.

## Batched background writer

A single background worker drains the channel and writes events to the per-process log file in batches of up to 128 events, issuing one `FlushAsync` per batch rather than per event. This amortizes file I/O cost across many events and keeps the writer's per-event overhead low without requiring the producer side to batch anything itself.

## Bounded, time-limited shutdown

`StopAsync(timeout)` stops accepting new events, completes the channel, and waits at most `timeout` for the background worker to drain and flush. If the worker has not finished by the time the timeout elapses, `StopAsync` returns anyway — it never blocks longer than the requested bound. The worker keeps running in the background in that case ("abandoned" from the caller's perspective); any exception it later raises is observed via a fire-and-forget continuation so it cannot surface as an unobserved task exception, but it cannot be propagated back to the (already-returned) `StopAsync` call without violating the time bound. `bLoggingStop` calls `StopAsync` with a 2-second timeout; the add-in's shutdown path (`AddInLifecycle.AutoClose`) also stops diagnostics with a bounded timeout before disposing the QuantLib runtime, so Excel is never kept open waiting on a slow or stuck diagnostics flush.

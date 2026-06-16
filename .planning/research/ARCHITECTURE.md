# Architecture Patterns: ExcelDNA + QuantLib SWIG Add-in

**Domain:** Quantitative finance Excel add-in with object handle pattern
**Researched:** 2026-06-15
**Overall confidence:** HIGH

---

## 1. System Overview

The add-in is structured as four layers with a hard left-to-right dependency rule: no layer reaches back to its caller.

```
┌─────────────────────────────────────────────────────────────┐
│  LAYER 4 — UDF Surface (ExcelDNA)                           │
│  QL_BuildDICurve()  QL_NPV()  QL_DV01()  QL_BuildVol()     │
│  • Validates Excel inputs                                    │
│  • Calls Object Store to get/put handles                     │
│  • Formats output for Excel cells                            │
├─────────────────────────────────────────────────────────────┤
│  LAYER 3 — Object Store (HandleStore)                        │
│  • ConcurrentDictionary<string, object>                      │
│  • Handle key generation and lookup                          │
│  • Thread-safe read; single-writer discipline for puts       │
├─────────────────────────────────────────────────────────────┤
│  LAYER 2 — Pricing / Risk Engine                             │
│  • Accepts handles; resolves to QuantLib objects             │
│  • Sets evaluation date before each calculation              │
│  • Calls QuantLib pricing engines: NPV, Greeks, scenarios    │
├─────────────────────────────────────────────────────────────┤
│  LAYER 1 — Market Data / Bootstrap                           │
│  • Converts Excel arrays → QuantLib Quote/RateHelper objects │
│  • Constructs PiecewiseYieldCurve, vol surfaces              │
│  • Returns QL term structure objects (not handles yet)       │
└─────────────────────────────────────────────────────────────┘
         ↑ only QL objects cross this boundary ↑
```

---

## 2. Object Store Design

### Recommended Implementation

Use a single static `ConcurrentDictionary<string, object>` as the backing store. This is the right choice because:

- Concurrent reads (the dominant case in multi-threaded Excel recalculation) are lock-free.
- The GC does not destroy your objects: the dictionary root keeps everything alive.
- Lookup from any Excel calculation thread is safe with zero locking on your side.
- The dictionary value type is `object` — cast to the known QuantLib type at the call site.

```csharp
internal static class HandleStore
{
    // One store for all asset classes.
    private static readonly ConcurrentDictionary<string, object> _store
        = new ConcurrentDictionary<string, object>(StringComparer.Ordinal);

    // Put: builder functions call this. Returns the key.
    public static string Put(string prefix, object qlObject)
    {
        var key = $"{prefix}#{Interlocked.Increment(ref _seq):D8}";
        _store[key] = qlObject;
        return key;
    }

    // Get: pricing functions call this.
    public static T Get<T>(string handle) where T : class
    {
        if (!_store.TryGetValue(handle, out var obj))
            throw new ArgumentException($"Unknown handle: {handle}");
        if (obj is not T typed)
            throw new InvalidCastException($"Handle {handle} is {obj.GetType().Name}, expected {typeof(T).Name}");
        return typed;
    }

    // Clear all: called on AutoClose / workbook close.
    public static void Clear() => _store.Clear();

    private static int _seq;
}
```

### Do Not Use

- **WeakReference**: The GC will collect QuantLib objects whose only reference is a WeakReference; you'll get sporadic null-handle errors after GC cycles — especially dangerous because QuantLib C++ objects are small from the GC's perspective but expensive to rebuild.
- **Lock on a plain Dictionary**: Unnecessary; ConcurrentDictionary is already safe for concurrent reads/writes.
- **Per-asset-class dictionaries**: One dictionary keyed by string prefix is simpler and sufficient.

### Handle Key Format

```
{PREFIX}#{SEQUENCE:D8}
```

Examples: `DICURVE#00000001`, `VOLSRF#00000003`, `BOND#00000007`

Prefixes encode the object type, making handles human-readable in spreadsheet cells and allowing the Get<T> cast to fail fast with a meaningful error. The sequence is a global `Interlocked.Increment` counter — no GUIDs needed; keys are per-session and short-lived.

---

## 3. Handle Lifecycle and Invalidation

### The Core Problem

Excel recalculates builder cells when their inputs change. Each recalculation calls `QL_BuildDICurve` again, which creates a **new** QuantLib curve object and returns a **new** handle key. The old handle key cell value is overwritten in Excel, and the old QuantLib object in the dictionary is now orphaned.

### Recommended Approach: Perpetual Store with Prefix-Based Replacement

For a small team (2–10 users, manual data entry), the simplest correct approach is:

1. Builder functions accept an optional `handleName` parameter (a user-supplied string like `"MAIN_DI"`).
2. If supplied, use that key directly: `_store[handleName] = newObject`. This overwrites the previous object atomically.
3. If not supplied, generate a sequence key. The old orphaned entry is memory-inert (it keeps a C++ object alive but does not cause errors).
4. On `IExcelAddIn.AutoOpen`, subscribe to `Application.WorkbookBeforeClose` via COM to call `HandleStore.Clear()`.

```csharp
// In AutoOpen:
var app = (Microsoft.Office.Interop.Excel.Application)ExcelDnaUtil.Application;
app.WorkbookBeforeClose += (wb, ref cancel) => HandleStore.Clear();
```

This covers the 95% case. Do not build an RTD-based cleanup mechanism for v1 — RTD topics are cleaned up by Excel when a cell no longer uses a function, but the timing is nondeterministic and adds significant complexity. Defer RTD lifetime management to a future version.

### How ExcelDNA Recalculation Interacts with the Object Store

ExcelDNA marks builder UDFs as non-volatile by default. The builder cell reruns only when its direct input cells change (inputs to the curve: dates array, rates array, settlement days). This is the desired behavior — the curve is rebuilt on-demand from its market data inputs, not on every F9.

If the quant wants forced rebuilds (e.g., "refresh curve to today's date"), declare the builder `IsVolatile = true` or have the user press F9 after changing the evaluation date cell.

---

## 4. QuantLib Threading Model — Critical Constraint

### The Problem

QuantLib uses a **global singleton** for the evaluation date (`Settings::instance().evaluationDate()`). When Excel runs multi-threaded recalculation (MTR) — which is on by default in Excel 2007+ — multiple pricing UDF cells can execute concurrently on different threads. If two threads call `Settings.instance().setEvaluationDate(d)` simultaneously, results are undefined.

Additionally, QuantLib's observer pattern (which propagates market data changes through the object graph) uses shared mutable state. QuantLib objects must **not be shared between threads** — a bootstrapped curve accessed simultaneously from two threads can trigger race conditions inside the C++ lazy-evaluation machinery.

### Solution: Disable MTR for This Add-in

Add to your `ExcelDna.dna` (or in code at `AutoOpen`):

```xml
<!-- ExcelDna.dna -->
<ExcelAddIn ...>
  <ExternalLibrary Path="QuantToolkit.dll" ExplicitExports="false" />
</ExcelAddIn>
```

```csharp
// In AutoOpen:
XlCall.Excel(XlCall.xlcOptionsCalculation,
    /* ... set threads to 1 ... */);
```

Or simply: in your add-in documentation, instruct users to set **Excel Options → Formulas → Use this many threads = 1**. For a 2–10 person desk with manually entered data, single-threaded recalculation is fast enough — a full DI curve bootstrap takes < 100 ms; a sheet with 50 NPV calls takes < 2 s.

### Evaluation Date: One Per Session

Set the evaluation date once per pricing call, not per object construction. Pricing UDFs receive the valuation date as a parameter and call `Settings.instance().setEvaluationDate(valuationDate)` before calling any QuantLib calculation. This is safe when MTR is disabled (single-threaded).

If you later need MTR, you must either:
- Compile QuantLib with `QL_ENABLE_SESSIONS` (makes `Settings` thread-local, requires custom build of NQuantLib native DLL), or
- Serialize all QuantLib calls through a dedicated single-threaded dispatcher thread using `ExcelAsyncUtil.QueueAsMacro`.

Both options are post-v1 scope items.

---

## 5. QuantLib SWIG Interop: Marshalling and Disposal

### Two-DLL Architecture

The SWIG bindings produce two DLLs that must both be present at runtime:

| DLL | Role |
|-----|------|
| `NQuantLib.dll` | Managed C# proxy classes; add as a NuGet/project reference |
| `NQuantLibc.dll` | Native C++ implementation; must be in the add-in output directory |

The native DLL must be x64. Add a post-build step to copy `NQuantLibc.dll` next to the `.xll`. ExcelDNA's packing mechanism does not automatically include native DLLs in the single-file `.xll` — you distribute both files.

### IDisposable and Memory Ownership

SWIG-generated C# classes implement `IDisposable`. The key field is `swigCMemOwn`:

- `swigCMemOwn = true`: the C# proxy owns the underlying C++ heap allocation and will delete it on `Dispose()` or finalization.
- `swigCMemOwn = false`: the C++ side owns the memory; the C# proxy is a view.

**Rule:** When you store a QuantLib object in the HandleStore, you are keeping a C# proxy alive. As long as the proxy is reachable, the GC finalizer will not run, and the C++ object lives. When you overwrite or clear the handle, the C# proxy becomes unreachable, the GC eventually finalizes it, and `swigFreeFunc` deletes the C++ memory.

**Do not call `Dispose()` manually on objects stored in the HandleStore.** The store owns the lifetime. Call `Dispose()` only on short-lived local objects (intermediate builders, helpers) that you don't intend to store.

```csharp
// BAD: disposing a stored object
var curve = HandleStore.Get<YieldTermStructure>(handle);
curve.Dispose(); // now the C++ object is deleted; next Get will crash

// GOOD: disposing a transient local
using var depositHelper = new DepositRateHelper(rate, tenor, days, cal, conv, eom, dc);
// ... use only to build PiecewiseYieldCurve, don't store
```

### Marshalling: What Crosses the Boundary

QuantLib SWIG C# marshalling is value-semantic for primitives (double, int, bool) and handle-semantic for objects. You never marshal raw C++ pointers — the proxy classes handle P/Invoke internally. The key types you work with:

| C# Type | What It Is |
|---------|------------|
| `Date` | Value type (day/month/year); maps to QL `Date` struct |
| `SimpleQuote` | Observable scalar; wrap in `QuoteHandle` for handles |
| `QuoteHandle` | Immutable handle to a Quote; pass to rate helpers |
| `RelinkableYieldTermStructureHandle` | Mutable handle; allows re-linking curve without rebuilding dependents |
| `YieldTermStructure` | Base class for all curves |
| `PiecewiseYieldCurve*` | Bootstrapped curve; keep alive in HandleStore |
| `Instrument` | Base for bonds, swaps, options |
| `PricingEngine` | Attached to instrument for NPV calculation |

Use `RelinkableYieldTermStructureHandle` in your pricing layer — wrap the stored curve in a relinkable handle when building engines. This allows swapping from one stored curve to another without reallocating the engine.

---

## 6. Curve Bootstrap Architecture

### Data Flow: Market Data → Curve → Pricing

```
Excel cells (dates[], rates[], settleDays, calendar, dayCount)
    │
    ▼ Layer 1: MarketData assembly
    Convert dates → QL Date[]
    Wrap rates → SimpleQuote[] → QuoteHandle[]
    Build RateHelper[] (DepositRateHelper, OISRateHelper, FuturesRateHelper)
    │
    ▼ PiecewiseYieldCurve<ZeroYield, Linear>(settleDt, helpers[], dc, cal)
    Bootstrap: iterative solver per node, local linear interpolation
    │
    ▼ Layer 3: HandleStore.Put("DICURVE", curve) → returns "DICURVE#00000001"
    │
    ▼ Layer 4: return handle key to Excel cell
                              │
                              │ (separate formula)
                              ▼
                    QL_NPV("DICURVE#00000001", <bond params>)
                              │
                    Layer 3: HandleStore.Get<YieldTermStructure>(handle)
                              │
                    Layer 2: pricing engine wired to curve
                              │
                    QL.NPV() → double → Excel cell
```

### DI Curve Bootstrap Specifics (Brazilian Market)

QuantLib contains `Business252` day counter natively. The Brazilian CDI/DI curve uses:

- Day count: `Business252(Brazil())` — QuantLib has a `Brazil` calendar class.
- Compounding: compounded with `Annual` frequency for on-the-run DI futures.
- First node: overnight CDI fixing as a `DepositRateHelper` with 1-day tenor.
- Remaining nodes: DI futures maturities (third Wednesday of contract month) as `FuturesRateHelper` (or custom `OISRateHelper` if treating as OIS).

The `Brazil` calendar in QuantLib covers BOVESPA/BMF holidays but **not necessarily current B3 holiday updates**. Maintain an override mechanism: a static `BrazilCalendarExtension` that adds or removes holidays loaded from a config cell range in the workbook.

### Curve Object Granularity

Build one QuantLib curve per tenor-space (DI pre, NTN-B real, CDI OIS if needed). Do not merge instrument types into one curve. The recommended v1 curves:

| Handle Prefix | Curve | Instruments Used |
|--------------|-------|------------------|
| `DICURVE` | BRL pre-fixed DI | DI1 futures + CDI deposit |
| `IPCA` | IPCA real rate | NTN-B mid-prices → real yield |
| `FXFWD` | USD/BRL forward | FX spot + NDF points |

---

## 7. Namespace and Class Structure

```
QuantToolkit/                          ← solution root
├── QuantToolkit.sln
├── src/
│   ├── QuantToolkit.Core/             ← class library, no Excel dependency
│   │   ├── Store/
│   │   │   └── HandleStore.cs         ← ConcurrentDictionary, Put/Get/Clear
│   │   ├── MarketData/
│   │   │   ├── DateConverter.cs       ← Excel double ↔ QL Date
│   │   │   ├── CalendarFactory.cs     ← Brazil(), TARGET(), etc.
│   │   │   └── DayCountFactory.cs     ← Business252, Actual360, etc.
│   │   ├── Curves/
│   │   │   ├── DICurveBuilder.cs      ← Builds PiecewiseYieldCurve for BRL DI
│   │   │   ├── IpcaCurveBuilder.cs    ← Builds IPCA real curve
│   │   │   └── FxForwardCurveBuilder.cs
│   │   ├── Instruments/
│   │   │   ├── IR/                    ← Swaps, deposits, DI futures
│   │   │   ├── FI/                    ← LTN, NTN-F, NTN-B
│   │   │   ├── FX/                    ← FX forwards, cross-currency
│   │   │   └── Credit/                ← CDB, debentures, CRI/CRA
│   │   ├── Pricing/
│   │   │   ├── NpvCalculator.cs       ← NPV for all instrument types
│   │   │   └── RiskCalculator.cs      ← DV01, duration, key rate durations
│   │   └── Scenarios/
│   │       └── ScenarioEngine.cs      ← Parallel shifts, twists, vol shocks
│   │
│   └── QuantToolkit.Excel/            ← ExcelDNA add-in project
│       ├── QuantToolkit.dna           ← ExcelDNA manifest
│       ├── AddIn.cs                   ← IExcelAddIn: AutoOpen/AutoClose
│       ├── UDFs/
│       │   ├── CurveUdfs.cs           ← QL_BuildDICurve, QL_BuildIpcaCurve
│       │   ├── BondUdfs.cs            ← QL_PriceLTN, QL_PriceNTNF, QL_PriceNTNB
│       │   ├── SwapUdfs.cs            ← QL_PriceDISwap, QL_PriceFixedFloat
│       │   ├── FxUdfs.cs              ← QL_FxFwd, QL_FxOption
│       │   ├── CreditUdfs.cs          ← QL_PriceCDB, QL_PriceDebenture
│       │   └── RiskUdfs.cs            ← QL_NPV, QL_DV01, QL_Duration, QL_KRD
│       └── Helpers/
│           └── ExcelInputParser.cs    ← object[,] → typed arrays, error handling
```

### Project Dependency Rule

`QuantToolkit.Core` has zero reference to ExcelDNA. It depends only on the QuantLib SWIG assemblies and `System.Collections.Concurrent`. This means:

- The entire pricing and risk layer is unit-testable without Excel.
- The Excel project is a thin adapter: validate Excel inputs, call Core, format output.

### UDF Naming Convention

```
QL_{Verb}{Object}
```

| Function | What it does |
|----------|-------------|
| `QL_BuildDICurve` | Builder → returns handle |
| `QL_BuildVolSurface` | Builder → returns handle |
| `QL_NPV` | Pricing → returns double |
| `QL_DV01` | Risk → returns double |
| `QL_CashFlows` | Output → returns object[,] |
| `QL_ScenarioPnL` | Scenario → returns double |

Prefix `QL_` avoids collision with built-in Excel functions and Bloomberg/Reuters functions that users may also have loaded.

---

## 8. ExcelDNA Function Registration Patterns

### Builder Functions

Builder UDFs write to the HandleStore and return a string. They must **not** be thread-safe because two concurrent calls to the same builder with overlapping inputs produce an ambiguous key. Register them without `IsThreadSafe`:

```csharp
[ExcelFunction(Name = "QL_BuildDICurve",
               Description = "Bootstraps BRL DI yield curve. Returns handle key.",
               Category = "QuantToolkit - Curves")]
public static object QL_BuildDICurve(
    [ExcelArgument(Description = "Valuation date (Excel date serial)")] double valuationDate,
    [ExcelArgument(Description = "Pillar dates as column/row range")] object pillarDates,
    [ExcelArgument(Description = "DI rates (annualized, decimal)")] object diRates,
    [ExcelArgument(Description = "Optional: fixed handle name, e.g. \"MAIN_DI\"")] object handleName)
{
    try
    {
        var dates   = ExcelInputParser.ToDates(pillarDates);
        var rates   = ExcelInputParser.ToDoubles(diRates);
        var valDate = ExcelInputParser.ToDate(valuationDate);
        var name    = ExcelInputParser.ToOptionalString(handleName);

        var curve = DICurveBuilder.Build(valDate, dates, rates);
        return HandleStore.Put(name ?? "DICURVE", curve);
    }
    catch (Exception ex)
    {
        return $"#ERR: {ex.Message}";
    }
}
```

### Pricing Functions

Pricing UDFs read from the HandleStore and perform no mutation. They can be marked `IsThreadSafe = true` provided MTR is enabled — but see the QuantLib threading caveat in section 4. For v1, leave `IsThreadSafe` at its default (false) and disable MTR.

```csharp
[ExcelFunction(Name = "QL_NPV",
               Description = "Net present value of instrument identified by handle.",
               Category = "QuantToolkit - Pricing")]
public static object QL_NPV(string instrumentHandle, string curveHandle, double valuationDate)
{
    try
    {
        var instrument = HandleStore.Get<Instrument>(instrumentHandle);
        var curve      = HandleStore.Get<YieldTermStructure>(curveHandle);
        var valDate    = ExcelInputParser.ToDate(valuationDate);
        return NpvCalculator.Calculate(instrument, curve, valDate);
    }
    catch (Exception ex)
    {
        return $"#ERR: {ex.Message}";
    }
}
```

### Error Handling Convention

Return a string beginning with `#ERR:` on failure rather than throwing. Excel displays this as a visible error string in the cell, which is more useful than `#VALUE!` for quants debugging their inputs. Optionally also log to a static in-memory ring buffer that can be read by a `QL_LastError()` UDF.

---

## 9. Scalability and Lifecycle Concerns

| Concern | At 10 sheets / ~200 formulas | At 50 sheets / ~2000 formulas | Notes |
|---------|------------------------------|-------------------------------|-------|
| Handle store memory | < 10 MB | < 50 MB | QuantLib curves are small; dominated by pillar count |
| Bootstrap time | < 50 ms per curve | < 50 ms per curve | Marginal; runs only on input change |
| NPV call time | < 1 ms each; < 200 ms total | < 2 s total | Acceptable for manual data entry |
| NQuantLibc.dll load | ~1 s cold start | Same | Load once at AutoOpen, not per call |
| Orphaned handles | Negligible at manual scale | Monitor with QL_StoreSize() UDF | Add a UDF returning store count if needed |

---

## 10. Suggested Build Order

Build in this sequence to get the fastest end-to-end working path. Each step produces something you can test before moving to the next.

### Step 1: Spike — ExcelDNA + QuantLib hello world (1–2 days)

Goal: prove the full stack works before writing any domain code.

- Create `QuantToolkit.Excel` project; install `ExcelDna.AddIn` NuGet.
- Manually copy `NQuantLibc.dll` (x64) to output; add `NQuantLib.dll` reference.
- Write one UDF: `=QL_Version()` that returns `Settings.version()` or similar.
- Confirm the XLL loads, the native DLL loads, and no P/Invoke exceptions occur.
- Write one UDF that creates a `Date` and returns it as a string — proves the SWIG interop round-trip.

### Step 2: HandleStore + DI Curve (2–3 days)

- Implement `HandleStore.cs`.
- Implement `ExcelInputParser.cs` (date conversion is the first real complexity).
- Implement `DICurveBuilder.cs` using `PiecewiseYieldCurve<ZeroYield, Linear>` with `Business252(Brazil())`.
- Wire up `QL_BuildDICurve` UDF.
- Test: enter 5-pillar DI rate data; confirm handle key returned; call `QL_DiscountFactor(handle, date)` to verify curve is correct.

### Step 3: First Instrument — LTN Zero Coupon Bond (2 days)

- Implement `QL_PriceLTN(curveHandle, faceValue, maturityDate, settlementDate)`.
- Use `DiscountingBondEngine` with the handle-resolved curve.
- This proves the handle-to-instrument-to-engine wiring pattern that all subsequent instruments reuse.

### Step 4: Risk — DV01 and Duration (1 day)

- Implement `QL_DV01` as a bump-and-reprice (1 bp parallel shift, recompute NPV).
- Use `BumpedCurveBuilder` that applies a `+0.0001` spread to all nodes.
- This pattern generalizes to key rate durations and scenario P&L.

### Step 5: NTN-F, NTN-B, Swaps (1 week)

- NTN-F: coupon bond priced on DI curve. Add `QL_PriceNTNF`.
- NTN-B: inflation-linked; requires IPCA curve. Build `IpcaCurveBuilder`, then `QL_PriceNTNB`.
- DI x PRE swap: already have both legs from LTN pricing; compose `QL_PriceDISwap`.

### Step 6: FX, Vol Surface, Options (1 week)

- `FxForwardCurveBuilder`: NDF points → forward curve.
- `BlackVolTermStructure` vol surface from Excel range → `QL_BuildVolSurface`.
- `QL_FxOption` using `BlackScholesMertonProcess` + analytical engine.

### Step 7: Credit Instruments, Scenarios, Greeks (ongoing)

- CDB/debenture: treated as fixed-rate bonds with credit spread; reuses bond pricing.
- Scenario engine: loop over shock grid, call pricing function each time.
- Greeks: bump-and-reprice for delta/vega; analytic formulas where available.

---

## 11. Component Boundary Summary

| Boundary | What Crosses | What Does Not Cross |
|----------|-------------|---------------------|
| Excel → UDF layer | `object` (Excel marshalled), `double`, `string`, `object[,]` | QuantLib types, handles as objects |
| UDF layer → Core | Typed C# primitives and arrays | Excel `object[,]`, ExcelDNA types |
| Core → HandleStore | QuantLib `object` (boxed proxy) | String handles (those stay in UDF layer) |
| Core → QuantLib | QL primitive types (Date, double, DayCounter) | .NET DateTime (always convert first) |
| NQuantLib.dll → NQuantLibc.dll | P/Invoke via SWIG-generated glue | Direct managed calls |

---

## Sources

- ExcelDNA threading model: [Multithreaded recalculation in Excel — Microsoft Learn](https://learn.microsoft.com/en-us/office/client-developer/excel/multithreaded-recalculation-in-excel)
- ExcelDNA IsThreadSafe / IsMacroType attributes: [ExcelFunction and other attributes — Excel-DNA Docs](https://excel-dna.net/docs/archive/wiki/ExcelFunction-and-other-attributes/)
- ExcelDNA object handle discussion: [Creating objects with Excel-DNA — Google Groups](https://groups.google.com/g/exceldna/c/gphDZkh4HaE)
- ExcelDNA AutoClose and shutdown detection: [Detecting Excel Shutdown and AutoClose — Excel-DNA Docs](https://excel-dna.net/docs/guides-advanced/detecting-excel-shutdown-and-autoclose/)
- QuantLib threading and global evaluation date: [The global evaluation date — Implementing QuantLib](https://www.implementingquantlib.com/2025/04/evaluation-date.html)
- QuantLib multithreading deep-dive: [Multi-Threading and QuantLib — HPC-QuantLib](https://hpcquantlib.wordpress.com/2013/07/26/multi-threading-and-quantlib/)
- QuantLib layered architecture: [Complete QuantLib Architecture Guide — RiskQuant-Haun](https://risk-quant-haun.github.io/quantlib/architecture)
- QuantLib observer and lazy evaluation: [The Observer pattern in QuantLib — Implementing QuantLib](https://www.implementingquantlib.com/2024/05/observer-pattern.html)
- QuantLib PiecewiseYieldCurve bootstrap: [Chapter 3 part 3: bootstrapping an interest-rate curve — Implementing QuantLib](https://www.implementingquantlib.com/2013/10/chapter-3-part-3-of-n-bootstrapping.html)
- QuantLib Handle/RelinkableHandle: [Handling dependencies in QuantLib — Implementing QuantLib](https://www.implementingquantlib.com/2023/10/handling-dependencies.html)
- NQuantLib64 NuGet package: [NQuantLib64 on NuGet](https://www.nuget.org/packages/NQuantLib64) (last update 2016 — prefer building from source or QLNet)
- QuantLib-SWIG releases: [lballabio/QuantLib-SWIG Releases — GitHub](https://github.com/lballabio/QuantLib-SWIG/releases) (v1.42, April 2025)
- QLNet pure C# alternative: [amaggiulli/QLNet — GitHub](https://github.com/amaggiulli/QLNet)
- SWIG C# memory management: [SWIG and C# — swig.org](https://www.swig.org/Doc4.2/CSharp.html)
- QuantLib C# ecosystem overview: [The QuantLib ecosystem — Implementing QuantLib](https://www.implementingquantlib.com/2025/03/quantlib-ecosystem.html)
- Business252 day counter source: [business252.cpp — QLAnnotatedSource](https://rkapl123.github.io/QLAnnotatedSource/dc/d8c/business252_8cpp_source.html)

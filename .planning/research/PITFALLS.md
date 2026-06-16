# Domain Pitfalls

**Domain:** ExcelDNA + QuantLib SWIG Excel add-in for Brazilian fixed income / derivatives
**Researched:** 2026-06-15
**Scope:** QuantLib SWIG interop, Brazilian market conventions, ExcelDNA patterns, .NET Core deployment

---

## Critical Pitfalls

Mistakes that cause silent mispricing, random crashes, or full rewrites.

---

### Pitfall 1: GC/Observer Race Condition — Random Crashes in Multi-Threaded Recalculation

**What goes wrong:** QuantLib's observer pattern is not thread-safe against the .NET garbage collector. The GC runs finalizers on a background thread. When Excel triggers multi-threaded recalculation (MTR) and the GC concurrently finalizes a QuantLib observer (e.g., a `QuoteHandle` or `RelinkableHandle`), the destructor fires at the exact moment the observable calls `update()` on the same object. Result: access violation, pure virtual function call, or silent heap corruption. Crashes are non-deterministic and hard to reproduce in isolation.

**Why it happens:** SWIG-wrapped C++ objects have `shared_ptr` ownership tracked on the C++ side but the C# wrapper class is GC-managed. When the C# wrapper loses its last reference and gets finalized, the C++ destructor runs on the GC finalizer thread — a different thread from the one currently executing an observer notification chain triggered by, for example, a market quote update. QuantLib assumes all observer operations happen on a single thread unless `QL_ENABLE_THREAD_SAFE_OBSERVER_PATTERN` is compiled in.

**Consequences:** Random `AccessViolationException` or silent wrong results when multiple UDFs recalculate simultaneously. Impossible to unit-test because it requires a specific GC timing. Manifests more frequently in Excel 365 where MTR is on by default and more threads are used.

**Prevention:**
1. **Compile QuantLib with `QL_ENABLE_THREAD_SAFE_OBSERVER_PATTERN`** (replaces observer internals with `boost::signals2`). This is the authoritative fix. Any pre-built NQuantLib binary that does not include this flag is unsafe for Excel MTR.
2. **Also compile with `QL_ENABLE_SESSIONS`** to make the `Settings` singleton (evaluation date, index fixings) thread-local rather than global — mandatory once MTR is in play.
3. Keep strong C# references to all QuantLib wrapper objects in your handle store for as long as the corresponding handle is live. Never rely on GC to determine object lifetime; keep a `ConcurrentDictionary<string, object>` that holds the wrapper alive.
4. Pin observer-participant objects explicitly or use `GC.KeepAlive()` at appropriate call sites.

**Detection:** Enable MTR in Excel (File → Options → Advanced → Formulas → "Enable multi-threaded calculation"). Run a stress sheet that triggers 50+ simultaneous handle-based recalculations. Any crash or `#VALUE!` storm that clears on single-thread mode points to this pitfall.

**Phase to address:** Phase 1 (infrastructure / handle store). Get the compiled QuantLib flags right before writing a single UDF. Retrofitting after the fact requires recompiling all native dependencies.

---

### Pitfall 2: Global Evaluation Date Singleton — Wrong Pricing Date Across Concurrent Cells

**What goes wrong:** `Settings::instance().evaluationDate()` is a process-wide singleton. If two UDFs run concurrently (ExcelDNA MTR) and one sets the evaluation date to price a forward scenario while another is mid-calculation, the second UDF prices at the wrong date. Even in single-threaded mode, setting the evaluation date in one cell triggers a notification cascade that forces recalculation of every lazy-evaluated object in the process — causing O(N) curve re-bootstraps where N is the number of open curves. Across a midnight boundary, the date changes silently with no notification sent, leaving curves stale.

**Why it happens:** QuantLib's `Settings` is a Meyers singleton; it holds a single `DateProxy`. There is no per-call or per-context isolation. ExcelDNA's MTR gives Excel multiple threads that all share this singleton unless `QL_ENABLE_SESSIONS` is compiled in.

**Consequences:** Scenario functions (e.g., DV01 by bumping the date) pollute the pricing date seen by all other concurrently recalculating cells. Workbooks that stay open overnight show stale prices at open because the evaluation date defaulted to "today" at load time but the internal date never updated. Subtle mispricing, not an obvious crash.

**Prevention:**
1. Compile with `QL_ENABLE_SESSIONS`. This turns the singleton into a thread-local, giving each Excel calculation thread its own evaluation date.
2. Always set the evaluation date **explicitly** at the top of every UDF that does date-sensitive work: `Settings.instance().setEvaluationDate(today)`. Never rely on the default "system date" behavior.
3. For scenario/shift UDFs that temporarily need a different date, create new QuantLib objects local to that call rather than mutating global state and restoring it — the restore triggers another notification cascade.
4. Add a `NOW()` or `TODAY()` volatile cell dependency in worksheets that price overnight to force recalculation at open.

**Detection:** Build a two-cell spreadsheet: one cell calls a DV01 function (which temporarily alters evaluation date), a neighboring cell calls NPV on the same curve. With MTR enabled, NPV will occasionally return the wrong value. Flip to single-threaded mode — the bug disappears.

**Phase to address:** Phase 1 (infrastructure). Must be resolved before any pricing UDF is built.

---

### Pitfall 3: No CETIP-Specific Calendar — Using the Wrong Holiday Set

**What goes wrong:** QuantLib's Brazil calendar (`Brazil::Settlement`) is a **banking** calendar, not a CETIP/B3 settlement calendar. The two differ. CETIP (now B3's OTC clearing arm, post-2017 merger) has its own holiday schedule that can diverge from the banking calendar on specific dates. Using `Brazil::Settlement` for DI futures and CDI swap settlement will produce wrong business-day counts on edge dates, leading to off-by-one errors in DU (dias úteis) calculations and mispriced accrual factors.

**Why it happens:** QuantLib has only two Brazil sub-calendars: `Settlement` (banking) and `Exchange` (BOVESPA equities). No CETIP-specific variant exists. The library's holiday data is also updated on a release cycle (quarterly), meaning recently decreed holidays — including newly added "Black Awareness Day" (November 20, added 2024) and any extraordinary closures — may be missing until the next release.

**Consequences:** A single missing or extra business day in a Business/252 accrual factor changes the rate by approximately 1/252 ≈ 0.4% — material on large notionals. DI futures DU mismatch means the quoted price does not reconcile with the market price. NTN-B coupon accrual periods come out wrong, producing wrong dirty prices.

**Prevention:**
1. Do not use raw `Brazil::Settlement` as the canonical CETIP calendar. Cross-reference QuantLib's built-in holiday list against the official B3/FEBRABAN published holiday schedule at project start.
2. Use `Calendar::addHoliday()` and `Calendar::removeHoliday()` at add-in startup to patch any discrepancies. Drive these patches from a managed list (e.g., a hidden Excel table or a JSON config file) so traders can update without a code release.
3. Track `QuantLib::Brazil` source changes on GitHub; subscribe to QuantLib release notes for calendar patches.
4. Validate DU counts for known dates (e.g., verify the number of business days in a known DI contract expiry period against B3's official DU published alongside contract specifications).

**Detection:** For each upcoming DI contract expiry, compare `Business252(Brazil()).dayCount(settlementDate, expiryDate)` against B3's published DU. Any mismatch is a calendar error.

**Phase to address:** Phase 2 (curve construction / DI futures). Must be resolved before any DI or CDI swap UDF ships. Requires an ongoing maintenance process, not just a one-time fix.

---

### Pitfall 4: Business/252 Performance — O(N) Day Iteration on Long Periods

**What goes wrong:** The naive Business/252 implementation iterates through every calendar day between two dates checking `isBusinessDay()`. For a 30-year NTN-B, that is ~10,950 calendar days, each requiring a holiday lookup. Called from hundreds of Excel cells across a bootstrapped curve, this makes the workbook feel unusable: a full recalculate can take 30–60 seconds.

**Why it happens:** QuantLib's `Business252` implementation caches monthly business-day counts to avoid the full iteration (it stores yearly and monthly counts). However, the cache is keyed per `Calendar` instance. If you construct a new `Business252(new Brazil())` per UDF call, the cache is cold every time. Since SWIG wrappers make C++ object construction look cheap, this mistake is easy to make.

**Consequences:** Bootstrap is already QuantLib's most expensive operation. Adding cold-cache Business/252 counting on top makes the first recalculate after market-data entry take tens of seconds. Users experience Excel as "frozen" and begin manual workarounds.

**Prevention:**
1. Construct calendar and day-counter instances **once** at add-in startup and share them via the handle store or static singletons. Never construct them inside a hot UDF path.
2. Warm the Business/252 cache at startup by computing `dayCount()` for a sufficiently large span (e.g., year 2000 to year 2060) to pre-populate monthly counts. This is a sub-second operation done once.
3. Cache bootstrapped curves aggressively: build the curve once when market data changes; downstream pricing functions should read from the handle, not rebuild. The object handle pattern in PROJECT.md is correct — enforce it rigorously.
4. Mark builder UDFs (e.g., `QL_BuildDICurve`) as non-volatile so they only rebuild when their input cells change, not on every F9.

**Detection:** Use Excel's built-in calculation profiler (Inquire add-in or manual timing via `=NOW()` delta) to identify which UDFs dominate recalculation time. If a builder UDF takes > 200ms, the cache is likely cold.

**Phase to address:** Phase 2 (curve bootstrap). Add performance benchmarks to the builder UDFs before shipping to users.

---

### Pitfall 5: IPCA Observation Lag — VNA Uses Prior Month's Index

**What goes wrong:** NTN-B's Updated Nominal Value (VNA) is indexed to the IPCA from the **last 15th of the month before the settlement date**, not the current IPCA. Before the 15th of a given month, the prior month's IPCA has not yet been officially incorporated; you must use a projection (ANBIMA publishes projections). QuantLib's `ZeroCouponInflationIndex` uses a configurable observation lag, but the default behavior and the correct Brazilian convention require explicit setup. Getting the lag wrong by one month produces a VNA error of approximately the monthly IPCA rate (recently 0.3–0.6%) applied to all cash flows.

**Why it happens:** Developers import an inflation index and assume `observationLag = 3 months` (the TIPS convention) or use QuantLib's default. Brazil's IPCA uses a 1-month effective lag with an additional intra-month interpolation rule tied to the 15th of the month, which is not captured by a simple lag parameter.

**Consequences:** All NTN-B cash flows are discounted with a wrong VNA base. Error compounds across all coupons and the principal. For a 30-year NTN-B, the price error from a one-month lag mistake can exceed 2% of par — far outside acceptable tolerance.

**Prevention:**
1. Define a custom `ZeroInflationIndex` for IPCA with the correct observation lag and fixing frequency (`Monthly`).
2. Implement the 15th-of-month rule explicitly: when the settlement date is on or after the 15th, use last month's IPCA; when before the 15th, apply ANBIMA's projection for the partial month. This logic sits outside QuantLib and must be written in your add-in.
3. Store historical IPCA fixings with the **first day of the reference month** as the key (e.g., the April IPCA release in mid-May is stored as key = May 1st minus lag). The QuantLib convention is `addFixing(firstDayOfReferenceMonth, value)`.
4. Validate VNA against ANBIMA's published VNA daily — the numbers must match to the last decimal.

**Detection:** Compare your computed VNA for today against ANBIMA's published VNA for the same date. Any non-trivial discrepancy (> R$0.01) indicates a lag configuration error.

**Phase to address:** Phase 3 (NTN-B pricing). Requires dedicated integration test against ANBIMA reference values before any NTN-B UDF ships to traders.

---

### Pitfall 6: DI Futures Price-Rate Convention Inversion

**What goes wrong:** DI1 futures are quoted as a **price** (P = 100,000 / (1 + rate)^(DU/252)), where DU is the number of business days between settlement and expiry. The relationship between price and rate is non-linear and inverted: higher rates mean lower prices. Implementing DV01 (or hedge ratios) in terms of price movement without converting back to rate space produces sign errors and wrong magnitudes. Additionally, the DU must use the **CETIP calendar** (see Pitfall 3), not a calendar-day count.

**Why it happens:** Developers familiar with bond pricing think in terms of yield → price. DI futures work the opposite way in how positions are typically sized and hedged. Furthermore, QuantLib's built-in `OvernightIndexFuture` or generic rate future classes do not map cleanly to the Brazilian DI1 contract specification; custom implementation is required.

**Consequences:** Hedge ratios for CDI swaps against DI futures come out wrong by a factor related to the contract's DU. A trader hedging a CDI swap books a position that does not offset the actual risk. Losses only discovered after a rate move.

**Prevention:**
1. Implement a dedicated `DI1Future` pricing function that takes the rate as input, converts to price, and computes sensitivities in rate space (not price space). Do not use generic futures pricing from QuantLib without wrapping the DI-specific convention.
2. Define DV01 as the change in present value for a 1-basis-point change in the DI rate, not the price. This requires differentiating through the price-rate formula.
3. Write unit tests that verify: given a known DI rate, the computed price matches B3's official settlement price for a published contract.

**Detection:** Compare your `QL_DIPriceToDIRate()` round-trip against B3's official DU and settlement price for a known contract expiry. Any basis-point discrepancy in the back-converted rate is a convention error.

**Phase to address:** Phase 2 (DI futures / curve bootstrap). The DI rate-to-price function is foundational to curve construction — if it is wrong, the bootstrapped curve is wrong.

---

## Moderate Pitfalls

---

### Pitfall 7: Handle Store Recalculation Order — Pricing Functions Evaluate Before Builder Functions

**What goes wrong:** Excel's dependency graph does not know that `QL_NPV(A1)` depends on the curve built by `QL_BuildDICurve(B2:B10)` unless the handle string from the builder is a direct cell reference in the pricing function's input. If a trader hardcodes the handle string (e.g., `"DI_CURVE_2026"`) instead of referencing the builder cell, Excel does not see the dependency, may evaluate the pricing function first, and returns either a stale result or `#VALUE!` because the handle does not yet exist in the store.

**Prevention:**
1. Builder functions must return the handle string to a cell. Pricing functions must reference that cell directly, not hardcode the handle name. Document this contract in every function's `ExcelArgument` description.
2. In the handle store's `Get` method, if a handle is not found, return a descriptive error string rather than throwing — this surfaces as `#VALUE!` with a message rather than a silent wrong number.
3. Consider adding a version or timestamp to each handle (e.g., `"DI_CURVE_20260615_143022"`) so stale handles become obvious when they do not match the builder's current output.

**Detection:** Build a test sheet where the pricing formula is evaluated before the builder by locking calculation order with `CTRL+ALT+F9` forced sequence. Any `#VALUE!` in the pricing cell that resolves after a second F9 indicates a missing dependency declaration.

**Phase to address:** Phase 1 (handle store design). Bake the dependency contract into the API from day one.

---

### Pitfall 8: IsMacroType UDFs Become Automatically Volatile — Cascade Recalculation

**What goes wrong:** ExcelDNA UDFs marked with `IsMacroType = true` (needed to accept `ExcelReference` arguments for dynamic arrays or range inspection) are automatically registered as volatile by Excel, even if `IsVolatile = false` is set. Volatile functions recalculate on every workbook change, not just when their inputs change. If a curve builder is accidentally marked as a macro-type function, it rebuilds the entire yield curve on every keystroke anywhere in the workbook.

**Prevention:**
1. Never mark builder functions as `IsMacroType = true`. Use it only for utility functions that genuinely need to inspect the calling cell (e.g., a `QL_Handle()` cell-reference helper).
2. Audit registered UDF attributes in the add-in before each release: check that no builder or pricing function inadvertently carries `IsMacroType`.
3. For functions that need to read a range, pass the range as a typed array (`double[]`) rather than an `ExcelReference` — this avoids the macro-type registration entirely.

**Detection:** Open Excel's Name Manager or use `xlGetName` to inspect the UDF registration flags. Any builder UDF showing as volatile is a bug.

**Phase to address:** Phase 1 (UDF registration scaffold). Establish the attribute policy before writing individual functions.

---

### Pitfall 9: Inflation Index Fixing Storage — Wrong Date Key Causes Silent Wrong Fixings

**What goes wrong:** QuantLib requires IPCA historical fixings to be stored with the **first day of the reference month** as the date key (e.g., the IPCA for April, released in mid-May, is added as `addFixing(Date(1, April, year), value)`). If developers use the release date or the 15th instead, `index.fixing(queryDate)` returns either zero or the wrong month's value without throwing an error.

**Prevention:**
1. Write a thin IPCA fixture loader that always normalizes the date to `Date(1, month, year)` before calling `addFixing`.
2. Add a validation step at add-in load: after populating fixings, query a known historical date and assert the returned value matches the published IPCA value.

**Detection:** Query `IPCA.fixing(Date(1, March, 2024))` — if it returns anything other than the official IBGE release for March 2024, the date key is wrong.

**Phase to address:** Phase 3 (NTN-B / inflation). Must be in place before any inflation curve bootstrap.

---

### Pitfall 10: .NET Core + SWIG Native Library Bitness and Runtime Mismatch

**What goes wrong:** ExcelDNA on .NET Core loads as a 64-bit in-process add-in. The SWIG-generated `NQuantLibc.dll` (the native C++ wrapper) must also be 64-bit and compiled against the same C runtime (UCRT / VC++ 2019 or 2022). A mismatch — e.g., a 32-bit `NQuantLibc.dll`, or one compiled against an older VC++ runtime not installed on the user's machine — causes a `DllNotFoundException` or `BadImageFormatException` at load time with no useful diagnostic.

**Prevention:**
1. Fix the target architecture to `x64` in the SWIG build and the ExcelDNA project. Disable "Any CPU" for all native-dependent assemblies.
2. Bundle the required Visual C++ Redistributable version in your deployment package, or use static linking of the C runtime in the native DLL build.
3. Ship a `QL_Version()` UDF as the very first function implemented — it calls into `NQuantLibc.dll` with no QuantLib logic. If it returns a version string, the native layer is loaded correctly. If it returns `#VALUE!`, the native load failed.
4. NativeAOT support in ExcelDNA (version 1.10 preview as of 2025) is experimental and explicitly unsupported with complex COM interop. Do not use NativeAOT for this add-in; use standard .NET Core runtime-dependent deployment.

**Detection:** The `QL_Version()` sentinel UDF returns `#VALUE!` on a clean machine that has not had other QuantLib software installed.

**Phase to address:** Phase 1 (infrastructure). The native layer must load correctly before any test can run.

---

### Pitfall 11: NTN-B Real Yield vs Nominal Yield — Quoting Convention Mismatch

**What goes wrong:** NTN-B yields are quoted as **real yields** (i.e., the yield over IPCA inflation, not the nominal yield inclusive of inflation expectations). Plugging a nominal yield into the NTN-B pricing formula produces a price that is wrong by approximately the inflation level (~4–6% annually). Conversely, back-solving a price to get the "yield" in a generic bond pricer returns a nominal yield that does not match the market convention.

**Why it happens:** QuantLib's generic bond pricing engines work with nominal yields. Inflation-linked bonds in QuantLib use a separate framework (`CPIBond`, `CPICoupon`). Without using the inflation-specific classes, a developer builds an NTN-B as a plain-vanilla bond with IPCA-adjusted coupons computed externally, which can work, but only if the yield parameter is treated as real and the cash flows already incorporate the VNA.

**Prevention:**
1. Use QuantLib's `CPIBond` class (or the equivalent inflation-linked bond class) rather than building NTN-B as a plain bond with manually computed coupons. This makes the real-yield convention explicit in the model.
2. Document in every NTN-B UDF's `ExcelArgument` description whether the yield input is real or nominal. Add an assertion that checks `yield < 0.20` as a sanity bound (real NTN-B yields are typically 4–8%).
3. Write a round-trip test: price an NTN-B, back-solve the yield, verify it matches the input real yield to 1e-6.

**Detection:** Price an NTN-B at a known yield and compare against ANBIMA's published price for the same bond on the same date. A discrepancy of several percent of par confirms a nominal/real mix-up.

**Phase to address:** Phase 3 (NTN-B). Capture this convention in design docs before implementation starts.

---

## Minor Pitfalls

---

### Pitfall 12: Business/252 Additivity — Accrual Periods Do Not Sum Correctly

**What goes wrong:** Business/252 day counts are not additive across sub-periods: `dayCount(d1, d3)` may not equal `dayCount(d1, d2) + dayCount(d2, d3)` if `d2` falls on a boundary involving a holiday. This matters for coupon accrual: summing daily accruals built from overnight compounding will differ from computing the period accrual directly if any intermediate date is a holiday.

**Prevention:** Always compute Business/252 factors for the full period, not as a product of sub-period factors. For CDI swap compounding (which compounds each overnight CDI rate), the compounding is correct by definition; the issue arises only if someone tries to decompose a coupon period into halves.

**Phase to address:** Phase 2 (CDI swaps).

---

### Pitfall 13: LTN Settlement — D+1 Business Day, Not D+0

**What goes wrong:** LTN (and NTN-F, NTN-B) settle T+1 business day in Brazil, not T+0 or T+2. If the settlement date is hardcoded to today, the accrual to settlement is zero instead of one business day, producing a slightly wrong price. The error is small (~0.01% of par) but accumulates in DV01 calculations.

**Prevention:** Always compute settlement as `calendar.advance(today, 1, BusinessDays)` and pass it explicitly to the bond pricer as the settlement date, not the evaluation date.

**Phase to address:** Phase 3 (bond pricing).

---

### Pitfall 14: QuantLib SWIG Date ↔ Excel Serial Number Conversion Off-By-One

**What goes wrong:** Excel uses a serial date system where January 1, 1900 = 1, but Excel incorrectly treats 1900 as a leap year (a Lotus 1-2-3 compatibility bug). Dates before March 1, 1900 are off by one. QuantLib uses its own `Date` class based on Julian day numbers. The conversion `QuantLib.Date.from_serial(excelSerial)` must account for the Excel leap-year bug; failing to do so causes all dates before 1900-03-01 to be wrong by one day. While pre-1900 dates are unlikely in live pricing, IPCA historical fixings may go back far enough to trigger this if a developer uses a date range that starts at the origin.

**Prevention:** Use a tested ExcelDNA date-to-QuantLib-Date conversion utility function and add a unit test for March 1, 1900, February 28, 1900, and January 1, 2000.

**Phase to address:** Phase 1 (utility layer).

---

### Pitfall 15: Curve Handle Invalidation After Index Fixing Update

**What goes wrong:** When a CDI or IPCA fixing is added (e.g., a trader updates today's CDI rate), QuantLib's observer pattern notifies all `RelinkableHandle`-wrapped curves and forces them to mark dirty. If the handle store contains live `RelinkableHandle<YieldTermStructure>` objects, adding one fixing causes every curve linked through those handles to re-bootstrap on the next pricing call. In a large workbook with many curves, this produces a recalculation wave even when the trader only changed one cell.

**Prevention:** Decouple fixing updates from live handle-linked curves. Keep fixing storage and curve construction separate: rebuild the curve explicitly (re-call the builder UDF) rather than relying on observer notifications to propagate. Treat the handle store as an explicit cache that only updates when the builder UDF re-runs.

**Phase to address:** Phase 2 (curve construction).

---

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|---|---|---|
| Phase 1: Handle store and UDF scaffold | GC/Observer race (Pitfall 1); evaluation date singleton (Pitfall 2); volatility attribute (Pitfall 8); native DLL mismatch (Pitfall 10); date conversion (Pitfall 14) | Compile QuantLib with correct flags before writing any UDF; implement QL_Version() sentinel; establish attribute policy |
| Phase 2: DI curve bootstrap | CETIP calendar accuracy (Pitfall 3); Business/252 performance (Pitfall 4); DI price-rate inversion (Pitfall 6); handle recalc order (Pitfall 7); fixing wave (Pitfall 15) | Validate DU counts against B3; benchmark builder UDF; enforce handle dependency discipline |
| Phase 3: Bond pricing (LTN, NTN-F, NTN-B) | IPCA lag (Pitfall 5); inflation fixing date key (Pitfall 9); real vs nominal yield (Pitfall 11); settlement T+1 (Pitfall 13); Business/252 additivity (Pitfall 12) | Integration test every bond type against ANBIMA reference prices; validate VNA daily |
| Phase 4: Scenario / Greeks | Evaluation date mutation for scenario pricing (Pitfall 2); handle invalidation wave (Pitfall 15) | Use local QuantLib objects for scenario bumps; do not mutate global state |
| Phase 5: Credit instruments (CDB, debentures) | Calendar accuracy for CETIP OTC (Pitfall 3); handle stale after rating change input | Same calendar validation process; ensure handle store invalidates on credit spread input change |

---

## Sources

- [QuantLib-SWIG Thread-Safe Observer Pattern (HPC-QuantLib)](https://hpcquantlib.wordpress.com/2012/02/27/quantlib-swig-and-a-thread-safe-observer-pattern-in-c/)
- [Multi-Threading and QuantLib (HPC-QuantLib)](https://hpcquantlib.wordpress.com/2013/07/26/multi-threading-and-quantlib/)
- [QuantLib 1.6.2 Multithreading Patch for JVM/.NET (HPC-QuantLib)](https://hpcquantlib.wordpress.com/2015/09/20/quantlib-1-6-2-multithreading-patch-for-jvm-net/)
- [The Global Evaluation Date (Implementing QuantLib)](https://implementingquantlib.substack.com/p/the-global-evaluation-date)
- [Odds and Ends: Global Settings (Implementing QuantLib)](https://www.implementingquantlib.com/2015/02/odds-and-ends-global-settings.html)
- [Holidays in QuantLib (Implementing QuantLib)](https://implementingquantlib.substack.com/p/holidays-in-quantlib)
- [QuantLib Brazil Calendar Class Reference](https://rkapl123.github.io/QLAnnotatedSource/d0/d04/class_quant_lib_1_1_brazil.html)
- [Inflation Indexes and Curves — QuantLib Guide](https://www.quantlibguide.com/Inflation%20indexes%20and%20curves.html)
- [Inflation Indexes and Curves (Implementing QuantLib)](https://www.implementingquantlib.com/2024/05/inflation-curves.html)
- [Implementing BUS/252 Day Count Convention (Clarus FT)](https://www.clarusft.com/implementing-bus252-daycount-convention/)
- [Dangerous Day-Count Conventions — QuantLib Guide](https://www.quantlibguide.com/Dangerous%20day-count%20conventions.html)
- [Brazilian DI1 Interest Rate Futures (SSRN, Burgess)](https://papers.ssrn.com/sol3/papers.cfm?abstract_id=4065845)
- [B3 DI Training: Brazilian Government Bonds](https://www.b3.com.br/en_us/products-and-services/trading/interest-rates/di-training-program/03-brazilian-government-bonds.htm)
- [Fix shared_ptr and directors for C# — SWIG GitHub Issue #2410](https://github.com/swig/swig/issues/2410)
- [ExcelDNA Multi-threading Issues Thread (Google Groups)](https://groups.google.com/g/exceldna/c/_XkjqX1xzn0)
- [ExcelDNA Accessing Workbook State in ThreadSafe UDF — GitHub Issue #366](https://github.com/Excel-DNA/ExcelDna/issues/366)
- [ExcelDNA NativeAOT Support](https://excel-dna.net/docs/guides-basic/dotnet-native-aot-support/)
- [ANBIMA Federal Government Bonds API](https://developers.anbima.com.br/en/documentacao/precos-indices/apis-de-precos/titulos-publicos/)
- [B3 Market Hours and Holidays](https://www.tradinghours.com/markets/bovespa)

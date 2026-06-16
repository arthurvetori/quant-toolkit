# Project Research Summary

**Project:** QuantLib Excel Add-in (QuantToolkit)
**Domain:** Quantitative finance Excel add-in — Brazilian fixed income, interest rate derivatives, FX, and credit instruments
**Researched:** 2026-06-15
**Confidence:** HIGH

## Executive Summary

This project is a production-grade Excel UDF library that wraps QuantLib's C++ analytics engine via official SWIG-generated C# bindings, surfaced through ExcelDNA. The canonical architecture — validated by QuantLibXL, Deriscope, and RQuantLib — is a strict two-phase handle pattern: builder functions bootstrap expensive QuantLib objects (yield curves, vol surfaces) once and return a string handle; pricing functions accept those handles, look up the object in a static `ConcurrentDictionary`, and compute results. Any deviation from this pattern (monolithic self-contained pricing functions, volatile UDFs, shared global date injection) leads to either catastrophic performance or silent mispricing.

The recommended stack is fully resolvable from NuGet: `ExcelDna.AddIn 1.9.0` + `QuantLib 1.42.1` (official SWIG bindings, maintained by Luigi Ballabio) on `net8.0-windows`, with `ExcelDna.IntelliSense 1.9.0` for UX and `Serilog` for structured logging. The project separates into two assemblies: `QuantToolkit.Core` (pure C#, no Excel dependency, fully unit-testable) and `QuantToolkit.Excel` (thin ExcelDNA adapter). This boundary is the single most important architectural decision — it enables test coverage of all pricing logic without Excel in the loop.

The dominant risk category is QuantLib's threading unsafety intersecting with Excel's multi-threaded recalculation (MTR). All QuantLib UDFs must be registered `IsThreadSafe = false`, and MTR must be disabled or carefully controlled. A secondary risk cluster is Brazilian market conventions: `Business252` with the Brazil Settlement calendar, exponential compounding, T+1 settlement, DI futures price-rate inversion, and IPCA observation lag are all non-negotiable correctness requirements where a single wrong assumption produces plausible-but-incorrect numbers that only manifest under rate moves.

---

## Key Findings

### Recommended Stack

| Package | Version | Purpose |
|---------|---------|---------|
| `ExcelDna.AddIn` | 1.9.0 | XLL host, MSBuild integration, single-file packing |
| `QuantLib` | 1.42.1 | Official C# SWIG bindings (maintained by Luigi Ballabio) |
| `ExcelDna.IntelliSense` | 1.9.0 | Formula-bar tooltips for UDFs |
| `ExcelDna.Diagnostics.Serilog` | 1.5.0 | Structured Excel-aware logging |
| `Serilog` | 4.x | Logger core |
| `Serilog.Sinks.File` | 5.x | Rolling-file sink |
| Target framework | `net8.0-windows` | Mandatory `-windows` suffix; .NET 8 LTS |

**Do not use:** `NQuantLib` (last updated 2012), `NQuantLib64` (last updated 2016, QL 1.8, .NET Framework only), `QLNet` (diverged API). `QuantLib 1.42.1` is the only actively maintained C# binding.

**Deployment:** Single packed `QuantToolkit-AddIn64-packed.xll` distributed to a shared folder. The native `NQuantLibc.dll` (x64, C++) must accompany the XLL — it cannot be embedded. NativeAOT explicitly ruled out for v1.

### Must-Have Features (Table Stakes)

1. **Object handle pattern** — `ConcurrentDictionary<string, object>` store; builder UDFs return string handles; all other features depend on this
2. **`QL_BuildDICurve`** — BRL DI curve from CDI overnight + DI1 futures + PRE×CDI swaps; `Business252(Brazil::Settlement)`; exponential compounding; T+1 settlement
3. **`QL_NPV` / `QL_DV01`** — generic handle-based pricing and bump-and-reprice sensitivity
4. **`QL_CashFlows`** — 2D `object[,]` array: payment dates, accrual periods, notionals, rates, nominal amounts, PV
5. **`QL_LTNPrice`, `QL_NTNFPrice`, `QL_NTNBPrice`** — scalar VNA input for NTN-B; correct real-yield convention
6. **`QL_DIFutureNPV` / `QL_DIFutureDV01`** — dedicated DI1 wrapper with price-rate inversion (`PU = 100,000/(1+r)^(DU/252)`)
7. **`QL_CDISwapNPV`** — PRE×CDI OIS swap
8. **`QL_FXForwardNPV`** — BRL leg `Business252`, USD leg `Actual/360`

### Differentiating Features (Should Have)

- `QL_KRD` — key rate durations across DI curve buckets
- `QL_BuildScenarioCurve` + `QL_ScenarioPnL` — parallel shift, steepener, twist, single-node shock
- `QL_FXOptionNPV` + `QL_FXOptionGreeks` — Garman-Kohlhagen vanilla FX options
- `QL_BuildLoan` — generic fixed/floating loan (CDI+, IPCA+, pre-fixed, SOFR+) with custom amortization
- Credit instruments — CDB, debentures, CRI/CRA as floating-rate bonds
- `QL_BondDuration` / `QL_BondConvexity` — matching ANBIMA reference figures

### Architecture

**Four-layer dependency:** UDF Surface → HandleStore → Pricing/Risk Engine → Market Data/Bootstrap

**Two assemblies:**
- `QuantToolkit.Core` — zero ExcelDNA dependency; `DICurveBuilder`, `NpvCalculator`, `RiskCalculator`, `ScenarioEngine`; fully unit-testable
- `QuantToolkit.Excel` — thin adapter; `ExcelInputParser`, `AddIn.cs`, UDF classes

**Handle key format:** `{PREFIX}#{SEQUENCE:D8}` (e.g., `DICURVE#00000001`). Optional user-supplied `handleName` for deterministic keys. Do not call `.Dispose()` on stored objects — the store owns object lifetime.

**UDF attribute policy:** All QuantLib UDFs `IsThreadSafe = false` (default). Builder UDFs must NOT set `IsMacroType = true` (would make them volatile). Disable Excel MTR for v1 (Tools → Options → Formulas → threads = 1).

### Critical Pitfalls

| # | Pitfall | Impact | Phase |
|---|---------|--------|-------|
| 1 | GC/observer race condition with MTR | Non-deterministic crashes | Phase 1 |
| 2 | Global evaluation date singleton | Concurrent pricing corruption | Phase 1 |
| 3 | Missing CETIP holidays in `Brazil::Settlement` | ~0.4% accrual factor error per day off | Phase 2 |
| 4 | Business/252 cold cache per UDF call | 30–60s full recalc on 30Y NTN-B workbook | Phase 2 |
| 5 | IPCA observation lag (15th-of-month rule) | ~0.3–0.6% per coupon mispricing | Phase 3 |
| 6 | DI futures price-rate inversion | Wrong-sign hedge ratios | Phase 2 |
| 7 | `IsMacroType = true` on builders | Cascade full recalc on every keystroke | Phase 1 |
| 8 | NTN-B real vs nominal yield | Several percent of par pricing error | Phase 3 |

---

## Roadmap Implications

### Suggested Phase Structure (5 phases, Coarse granularity)

**Phase 1 — Infrastructure Spike and Handle Store**
Prove the full stack loads on a clean machine. Establish `HandleStore`, `ExcelInputParser`, logging, and UDF attribute policy before any domain code. Resolve threading model and native DLL loading.
- Delivers: working `.xll`; `QL_Version()` sentinel; `HandleStore`; Serilog rolling log
- Prevents: Pitfalls 1, 2, 7 (must be fixed in Phase 1 — retrofitting requires full rewrite)

**Phase 2 — DI Curve Bootstrap and Generic Pricing**
The DI curve is the root dependency for all BRL pricing. Validate correctness (DU counts, calendar, DI price-rate convention) before any instrument pricer is written.
- Delivers: `QL_BuildDICurve`, `QL_NPV`, `QL_DV01`, `QL_CashFlows`, CETIP calendar patching
- Gate: DU counts match B3 official; bootstrapped curve reproduces known swap rates within 0.1bp
- Prevents: Pitfalls 3, 4, 6

**Phase 3 — Brazilian Government Bonds and CDI Swaps**
Daily desk instruments. LTN and NTN-F are straightforward once the curve exists. NTN-B requires dedicated IPCA lag design before coding.
- Delivers: `QL_LTNPrice`, `QL_NTNFPrice`, `QL_NTNBPrice`, `QL_CDISwapNPV`, `QL_DIFutureNPV`
- Gate: all instruments match ANBIMA reference prices before shipping to traders
- Prevents: Pitfalls 5, 8

**Phase 4 — FX and Generic Instruments**
Standard QuantLib patterns. USD curve builder, FX forwards, vanilla FX options, generic loan builder.
- Delivers: `QL_BuildUSDCurve`, `QL_FXForwardNPV`, `QL_FXOptionNPV`, `QL_BuildLoan`

**Phase 5 — Advanced Risk and Credit Instruments**
Key rate durations, scenario P&L, and credit instruments complete the desk toolkit.
- Delivers: `QL_KRD`, `QL_BuildScenarioCurve`, `QL_ScenarioPnL`, `QL_CDBNPVFromHandle`, `QL_DebentureNPV`, `QL_BondDuration`, `QL_BondConvexity`

### Research Phase Flags

Phases needing deeper research before planning:
- **Phase 2** — DI1 `FuturesRateHelper` adaptation is non-standard; CETIP holiday patching design
- **Phase 3** — IPCA 15th-of-month observation lag is not covered by QuantLib's standard lag parameter; requires custom C# logic
- **Phase 5** — KRD bucket-bumping via `SpreadedLinearZeroInterpolatedTermStructure` needs validation for Brazilian DI curve interpolation

Phases with standard patterns (skip research phase):
- **Phase 1** — ExcelDNA + `ConcurrentDictionary` handle store; well-documented
- **Phase 4** — Standard `FxForwardHelper`, `AnalyticEuropeanEngine`, `FixedRateBond`/`FloatingRateBond`

---

## Open Questions (resolve in Phase 1 and 2)

1. **`QL_ENABLE_THREAD_SAFE_OBSERVER_PATTERN` in NuGet build** — Must stress-test MTR with `QuantLib 1.42.1` NuGet. If not compiled in, options are: (a) disable MTR entirely (v1 recommendation), or (b) build from source (Boost + CMake + SWIG 4.3 toolchain — significant burden).
2. **`NQuantLibc.dll` redistribution** — Confirm whether the packed XLL auto-includes it or requires manual co-location.
3. **`CPIBond` in C# SWIG surface** — Available in Python SWIG; must verify C# binding at Phase 3 start. Fallback: manual VNA-adjusted cash flow construction.
4. **CETIP calendar exact divergence** — Cross-reference B3/FEBRABAN published holiday schedules against `Brazil::Settlement` before Phase 2.
5. **DI1 `FuturesRateHelper` adaptation** — Exact implementation of `100,000/(1+r)^(DU/252)` price-to-rate conversion as a QuantLib rate helper; design spike needed in Phase 2.

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack (packages/versions) | HIGH | Confirmed from NuGet gallery 2026-06-15 |
| Features | HIGH | Brazilian conventions verified against B3, ANBIMA, Clarus FT, QuantLib source |
| Architecture | HIGH | Validated by QuantLibXL, Deriscope, HPC-QuantLib |
| Pitfalls | HIGH | Threading: QuantLib issue tracker + HPC-QuantLib; conventions: B3/ANBIMA docs |

---

*Research completed: 2026-06-15 | Ready for roadmap: yes*

# QuantLib Excel Add-in (ExcelDNA / .NET)

## What This Is

An Excel add-in built with ExcelDNA (.NET Core) and QuantLib SWIG bindings that exposes a library of User Defined Functions (UDFs) for pricing and risk management directly in Excel. It targets a small desk of quants and traders who build pricing and risk spreadsheets for Brazilian and international interest rate, fixed income, FX, and credit instruments.

## Core Value

A composable, handle-based UDF library that lets the team price any instrument and compute sensitivities without leaving Excel — using QuantLib-grade analytics with Brazilian market conventions baked in.

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] Object handle pattern: curve/vol surface builder functions return opaque handles; pricing and risk functions accept handles as inputs
- [ ] Yield curve construction for Brazilian DI/CDI curves (business/252 day count, CETIP calendars)
- [ ] Government bond pricing: LTN (zero coupon pre-fixed), NTN-F (NTN-F coupon bond), NTN-B (IPCA inflation-linked)
- [ ] DI futures and PRE x CDI swap pricing
- [ ] Generic fixed/floating loan and deposit pricing
- [ ] FX forwards and cross-currency (BRL/USD and other pairs)
- [ ] FX options pricing (vanilla)
- [ ] Credit instruments: CDB, debentures, CRI/CRA pricing
- [ ] Mark-to-market / fair value (NPV) output for all instruments
- [ ] Greeks: delta, gamma, vega where applicable
- [ ] Yield curve sensitivities: DV01, duration, convexity, key rate durations
- [ ] Scenario / stress P&L: parallel shifts, curve twists, vol surface shocks
- [ ] Gap / cash flow analysis output
- [ ] Market data entered manually in Excel cells (no live feed dependency for v1)

### Out of Scope

- Live market data feed (Bloomberg RTD, Reuters) — deferred to post-v1; manual input covers v1 needs
- Exotic structured products (beyond vanilla options) — not in initial scope
- Credit default swaps / CDS curves — deferred
- Multi-curve OIS/LIBOR discounting for non-BR instruments — deferred; BR curve bootstrap is priority
- Excel ribbon / custom task pane UI — functions-first; no custom UI in v1
- Report generation or PDF export — not in scope

## Context

- Runtime: ExcelDNA with .NET Core (not Framework); SWIG-generated C# bindings for QuantLib
- Handle pattern: builder functions (e.g., `QL_BuildDICurve`, `QL_BuildVolSurface`) cache QuantLib objects in a static/thread-safe object store and return string handle keys; downstream functions (e.g., `QL_NPV`, `QL_DV01`) accept those handles
- Brazilian market specifics: business/252 day count convention, Brazil/CETIP business calendar, IPCA inflation index for NTN-B, CDI fixing for floating legs
- Team size: 2-10 quants/traders sharing the add-in; deployed as a shared `.xll` or `.xlam`
- QuantLib SWIG: NQuantLib or equivalent .NET SWIG wrapper; may require custom extensions for Brazilian conventions not natively in QuantLib

## Constraints

- **Tech Stack**: ExcelDNA + .NET Core + QuantLib SWIG — fixed; no alternative runtimes
- **Data Entry**: Manual cell input for market data in v1 — no external data connectors
- **Deployment**: Must work as a single distributable add-in file for the team; no server-side dependency
- **Brazilian conventions**: Must correctly implement business/252, CETIP calendar, and IPCA/CDI fixings — incorrect conventions invalidate all pricing

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| Object handle pattern over self-contained functions | Composability — curves built once, reused across many pricing calls; avoids redundant bootstrap per cell | — Pending |
| ExcelDNA over VSTO / COM add-in | ExcelDNA is the standard for .NET Excel add-ins; direct UDF registration, async support, no interop overhead | — Pending |
| QuantLib SWIG over managed re-implementation | QuantLib is the industry standard quant library; SWIG bindings give access to full pricing engine without reimplementing math | — Pending |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-06-15 after initialization*
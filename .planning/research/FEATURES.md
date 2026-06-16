# Feature Landscape: QuantLib Excel Add-in (ExcelDNA + QuantLib SWIG)

**Domain:** Quant finance Excel add-in — pricing and risk for interest rate derivatives, Brazilian fixed income, FX, and credit instruments
**Researched:** 2026-06-15
**Research mode:** Ecosystem + domain-specific conventions

---

## Table Stakes

Features that users of a production quant add-in expect unconditionally. Missing any of these causes the add-in to be perceived as a prototype rather than a tool, and the team reverts to manual pricing in Python scripts or Bloomberg DAPI.

### 1. Object Handle Pattern (Curve / Surface Builders Return Handles)

**Why expected:** Every production QuantLib integration — QuantLibXL, Deriscope, RQuantLib — uses this pattern. The core insight from QuantLib's ObjectHandler design (QuEP 11) is that financial objects (yield curves, vol surfaces, index fixings) are expensive to construct and must be shared across dozens of dependent pricing calls in the same sheet. Self-contained functions that re-bootstrap the curve on every cell evaluation are unusable in practice — a 50-instrument workbook would be 50x slower and produce inconsistent results mid-recalculation.

**Concrete shape:**
- Builder UDFs (e.g., `QL_BuildDICurve`, `QL_BuildBlackVolSurface`) store QuantLib objects in a static `ConcurrentDictionary<string, object>` keyed by a deterministic handle string. They return the handle string to the cell.
- Pricing and risk UDFs (e.g., `QL_NPV`, `QL_DV01`, `QL_CashFlows`) accept handle strings as inputs, look up the object, and compute.
- Handle invalidation: the handle string should encode a hash of the builder's input arguments so that changing a market rate automatically triggers a new handle key, forcing downstream cells to recalculate. Do not use mutable-state invalidation.

**Anti-state note:** The object store must be append-only during a calculation cycle. Never mutate a stored curve object in place; always build a new one and return a new handle. This prevents one pricing call from corrupting another's curve reference.

### 2. Yield Curve Construction — DI/CDI Pre Curve (Brazilian Priority)

**Why expected:** All Brazilian interest rate pricing — swaps, DI futures, government bonds, CDB/debentures — requires a bootstrapped DI curve. Without it, nothing prices correctly.

**Bootstrapping approach:** Piecewise flat-forward (log-linear discount factor) interpolation is the Brazilian market standard. It matches B3/ANBIMA reference curves. Use QuantLib's `PiecewiseLogLinearDiscount` or `PiecewiseLinearForward`.

**Instrument inputs for the DI curve** (in term order, shortest to longest):

| Tenor range | Instrument | QuantLib helper | Key convention |
|-------------|------------|-----------------|----------------|
| Overnight to ~3 months | CDI overnight fixing | `DepositRateHelper` | Business/252, Brazil Settlement calendar |
| 1 month to 2 years | DI futures (B3) | `FuturesRateHelper` | Prices quoted as 100000 / (1 + rate)^(DU/252); convexity adj. optional in v1 |
| 2 years to 10+ years | PRE x CDI fixed-for-floating swaps | `OISRateHelper` or custom `SwapRateHelper` | Fixed leg: annual, Business/252; floating: CDI daily compounding |

**Concrete UDF signature pattern:**
```
=QL_BuildDICurve(
    settlementDate,     -- date or "today"
    futuresDates,       -- vertical range: expiry dates
    futuresPrices,      -- vertical range: B3 DI future prices (e.g. 89500.00)
    swapTenors,         -- vertical range: "2Y","3Y","5Y","7Y","10Y"
    swapRates,          -- vertical range: annualized rates (e.g. 0.1250)
    [overnightRate]     -- scalar: today's CDI fixing
)
-- returns: handle string e.g. "DICurve_20260615_a3f9"
```

**Brazilian conventions that MUST be correct:**
- Day count: `Business252` with `Brazil::Settlement` calendar — this is not Actual/360 or Actual/365
- Rate compounding: exponential (Brazilian convention) — `(1 + r)^(DU/252)`, not simple or continuous
- Settlement: T+1 for DI instruments (not T+2 as in USD swaps)
- DI futures expiration: always the first business day of the expiry month

### 3. Yield Curve Construction — Generic (USD/EUR curves for FX and cross-currency)

**Why expected:** FX forwards and options require a discount curve for the foreign currency. Without this, FX NPV is wrong.

**Instrument inputs:**

| Instrument | QuantLib helper | Typical tenors |
|------------|-----------------|----------------|
| Eurodollar / SOFR deposits | `DepositRateHelper(rate, tenor, fixingDays, calendar, convention, endOfMonth, dayCounter)` | O/N, 1W, 1M, 3M |
| SOFR futures | `SofrFutureRateHelper` or `FuturesRateHelper` | IMM dates, 1–2 years |
| USD OIS swaps | `OISRateHelper(settlementDays, tenor, fixedRate, overnightIndex, discountingCurve, ...)` | 2Y–30Y |
| FRA | `FraRateHelper` | 1x4, 3x6, 6x12 |

Interpolation: `PiecewiseLogLinearDiscount` (log-linear on discount factors) for USD curves. This matches Bloomberg's default.

### 4. NPV / Fair Value for All Instruments

**Why expected:** NPV is the universal output. Without it the add-in cannot be used for P&L or mark-to-market. Every instrument must produce a single scalar NPV in the base currency.

**UDF pattern:**
```
=QL_NPV(instrumentHandle, discountCurveHandle, [forecastCurveHandle])
-- returns: scalar NPV in currency of instrument
```

Separate `discountCurveHandle` and `forecastCurveHandle` for floating-rate instruments (standard multi-curve setup in QuantLib). For Brazilian CDI swaps, both are typically the same DI curve.

### 5. DV01 / Dollar Duration

**Why expected:** This is the most basic rate sensitivity metric. Traders size hedges using DV01. Without it, the add-in cannot be used for risk management.

**Implementation:** Finite difference — bump the curve by 1 bp (+0.0001), reprice, then `DV01 = (NPV_up - NPV_base) / 1`. Return as a scalar per instrument.

```
=QL_DV01(instrumentHandle, curveHandle)
-- returns: scalar, change in NPV for 1bp parallel shift
```

### 6. Cash Flow Schedule / Leg Analysis

**Why expected:** Traders need to see the full amortization / coupon schedule for every instrument to verify pricing, check dates, and build cash flow reports. This is a foundational audit tool.

**Output shape:** This UDF must return an array (ExcelDNA supports `object[,]` return for array formulas). Each row is one cash flow event. Columns:

| Column | Description |
|--------|-------------|
| Payment Date | Date the cash flow settles |
| Accrual Start | Start of accrual period |
| Accrual End | End of accrual period |
| Accrual Days | Business days in period (for Business/252) |
| Notional | Principal on which coupon accrues |
| Rate | Coupon rate (fixed) or projected rate (floating) |
| Amount (nominal) | Coupon amount in notional currency |
| Amount (PV) | Discounted present value of cash flow |
| Type | "Fixed", "Floating", "Principal", "Inflation" |

This matches the QuantLibXL `qlSwapLegAnalysis` output pattern, extended with PV column.

**UDF pattern:**
```
=QL_CashFlows(instrumentHandle, discountCurveHandle)
-- enter as array formula; returns table of cash flows
```

### 7. Government Bond Pricing: LTN, NTN-F, NTN-B

**Why expected:** These are the core instruments of the Brazilian fixed income market. The desk prices them daily.

**LTN (Letra do Tesouro Nacional — zero coupon pre-fixed):**
- Formula: `PU = 1000 / (1 + r)^(DU/252)` where DU = business days to maturity, r = yield to maturity (annualized, exponential)
- UDF inputs: `settleDate`, `maturityDate`, `yield` → outputs `PU` (unit price) and `NPV`
- Duration: `DU/252 / (1+r)` (modified, in years on a Business/252 basis)

**NTN-F (coupon bond, pre-fixed 10% semi-annual coupon):**
- Semi-annual coupons of BRL 48.81 on face R$1000, plus principal at maturity
- Each coupon discounted at `(1+r)^(DU_i/252)` using business days to that coupon date
- UDF inputs: `settleDate`, `maturityDate`, `yield` → `PU`, `NPV`, `accrued`, `clean price`

**NTN-B (IPCA inflation-linked coupon bond):**
- VNA (Valor Nominal Atualizado) = R$1000 × accumulated IPCA from base date (July 15, 2000) to reference date
- Semi-annual coupon = 6% p.a. on VNA, paid on specific ANBIMA coupon dates
- UDF inputs: `settleDate`, `maturityDate`, `yield` (real yield), `currentVNA` (manual input from ANBIMA) → `PU`, `NPV`
- The `currentVNA` must be provided as a cell input — do not embed index logic in the function

### 8. DI Futures Pricing

**Why expected:** DI futures are the primary hedging instrument on the Brazilian desk. Pricing and DV01 must be correct to within B3 rounding.

**Contract mechanics:** Face value BRL 100,000. Price quoted as `PU = 100000 / (1+r)^(DU/252)`. One contract = 1 PU. DV01 = 1 bp shift in yield × DU/252 term.

**UDF inputs:**
```
=QL_DIFutureNPV(expiryDate, contractPrice, units, todayCurveHandle)
=QL_DIFutureDV01(expiryDate, curveHandle)
```

### 9. PRE x CDI Swap Pricing

**Why expected:** The most liquid OTC instrument in Brazil. Pricing is direct from the DI curve.

**Mechanics:**
- Fixed leg: annual rate, exponential compounding, Business/252, single bullet payment at maturity
- Floating leg: accumulated CDI from start to maturity, compounded daily on business days only
- Settlement: T+1, CETIP/B3 calendar

**UDF inputs (minimum viable):**
```
=QL_CDISwapNPV(
    tradeDate,
    maturityDate,
    notional,
    fixedRate,         -- annualized pre rate
    position,          -- "pay" or "receive" fixed
    diCurveHandle
)
```

### 10. FX Forwards Pricing

**Why expected:** Basic FX risk management requirement.

**Formula:** `F = S × (1 + r_BRL)^(DU_BRL/252) / (1 + r_USD)^(t_USD/360)` where the BRL side uses Business/252 and the USD side uses Actual/360 (or the appropriate foreign convention). Cross-currency basis is deferred (out of scope v1).

**UDF inputs:**
```
=QL_FXForwardNPV(
    settleDate,
    maturityDate,
    notional,
    contractFXRate,    -- agreed USD/BRL rate
    spot,              -- today's USD/BRL spot
    domCurveHandle,    -- BRL DI curve
    forCurveHandle     -- USD OIS curve
)
```

---

## Differentiators

Features that set this add-in apart from generic QuantLib wrappers and generic Excel quant tools. Not expected by a naive user, but provide significant competitive advantage for this desk.

### 1. Key Rate Durations (Bucket Sensitivities)

**Value:** Standard DV01 shows aggregate rate sensitivity; key rate durations decompose that sensitivity across specific maturity buckets (e.g., 1Y, 2Y, 3Y, 5Y, 7Y, 10Y DI curve nodes). This is essential for yield curve hedging — knowing that a position has risk concentrated in the 5Y bucket vs. the 10Y bucket completely changes the hedge.

**Implementation:** Use QuantLib's `SpreadedLinearZeroInterpolatedTermStructure` or equivalent. For each bucket node `i`, construct a curve where only the zero rate at node `i` is bumped by `delta` (e.g., 1 bp), then compute `(NPV_up - NPV_base) / delta`. Return as an array: one KRD value per node.

**UDF pattern:**
```
=QL_KRD(instrumentHandle, curveHandle, bucketDates)
-- returns: array of KRD values aligned to bucketDates
-- sum of KRDs ≈ modified duration
```

This mirrors the Deriscope KRD implementation but directly exposed as a native UDF.

### 2. Scenario / Stress P&L

**Value:** Traders need to answer "what happens to my book if the DI curve shifts 100bp parallel?" or "if the curve twists steepens by 50bp at the 10Y bucket?" This is the central risk management workflow.

**Scenario types to support:**

| Scenario type | Description |
|---------------|-------------|
| Parallel shift | All curve nodes shift by ±N bp |
| Steepener / flattener | Short end unchanged, long end +N bp (or vice versa) |
| Twist | Short end −N, long end +N (butterfly), or any linear gradient |
| Single node shock | One bucket bumped by N bp, others unchanged |
| FX spot shock | Spot rate moves by N% |
| Vol surface shock | ATM vol shifts by N bp (for options) |

**UDF pattern — two-function design:**
```
=QL_BuildScenarioCurve(baseCurveHandle, shiftType, shiftMagnitude, [shiftTenors])
-- returns: scenarioCurveHandle

=QL_ScenarioPnL(instrumentHandle, baseCurveHandle, scenarioCurveHandle)
-- returns: scalar P&L = NPV(scenario) - NPV(base)
```

Keeping scenario curve construction separate from P&L computation lets users chain: one scenario curve, many instruments.

### 3. Vanilla FX Options (Black-Scholes, Garman-Kohlhagen)

**Value:** FX options are in scope for v1. Distinguishes the add-in from rate-only tools.

**QuantLib engine:** `AnalyticEuropeanEngine` with Garman-Kohlhagen process (treated as equity with continuous dividend = foreign risk-free rate). Inputs: spot, strike, domRate, forRate, vol, expiry, notional.

**Greeks:** delta, gamma, vega, theta — all via QuantLib's analytic engine.

```
=QL_FXOptionNPV(spot, strike, expiry, vol, domCurveHandle, forCurveHandle, optionType, notional)
=QL_FXOptionGreeks(spot, strike, expiry, vol, domCurveHandle, forCurveHandle, optionType)
-- returns: array [delta, gamma, vega, theta, rho]
```

### 4. Swaption and Cap/Floor Pricing (IR Volatility)

**Value:** Required for any desk doing options on rates. The vol surface object pattern (separate builder function) is the key — it lets the user calibrate a vol surface once and price many options against it.

**QuantLib approach:**
- Build vol surface: `QL_BuildSwaptionVolCube(optionTenors, swapTenors, vols, volType)` → handle
- Build cap/floor vol surface: `QL_BuildCapFloorVolSurface(expiries, strikes, vols)` → handle
- Price: `QL_SwaptionNPV(swaptionHandle, curveHandle, volHandle)` / `QL_CapFloorNPV(...)`

Black (lognormal) and Bachelier (normal) vol conventions both required — Brazilian swaption market uses both.

### 5. Convexity and Modified Duration Output

**Value:** Standard duration analytics alongside NPV. Allows direct comparison with ANBIMA reference data for government bonds.

```
=QL_BondDuration(instrumentHandle, curveHandle, durationConvention)
-- durationConvention: "Modified", "Macaulay", "Simple"
=QL_BondConvexity(instrumentHandle, curveHandle)
```

For LTN/NTN-F/NTN-B, output should match ANBIMA's published duration figures to < 0.001Y tolerance.

### 6. Generic Fixed/Floating Loan and Deposit Pricing

**Value:** Directly in scope. Generalized enough to handle CDI+ floating, pre-fixed, IPCA+, or USD SOFR+ instruments with custom amortization schedules.

**UDF pattern:**
```
=QL_BuildLoan(
    settleDate,
    maturityDate,
    notional,
    couponType,          -- "Fixed", "CDI_Plus", "IPCA_Plus", "SOFR_Plus"
    couponRate,          -- annualized rate or spread
    frequency,           -- "Monthly","Quarterly","Semiannual","Annual","Bullet"
    amortizationSchedule,-- range of (date, notional) pairs, or "Bullet"
    dayCountConvention,  -- "Business252","ActActISDA","Act360"
    calendar             -- "Brazil","TARGET","UnitedStates"
)
-- returns: instrumentHandle
```

Then price with standard `QL_NPV`, `QL_DV01`, `QL_CashFlows`.

### 7. Credit Instrument Pricing: CDB, Debentures, CRI/CRA

**Value:** These are priced as floating-rate bonds with a CDI spread or fixed pre rate. The differentiator is correct Brazilian convention (Business/252, CETIP settlement, daily CDI compounding).

**Typical structures:**
- CDB: floating at X% × CDI or CDI + Y bp; bullet maturity; Business/252
- Debenture: may be fixed pre, CDI+, IPCA+, or IPCA× (index ratio); semi-annual or annual coupons
- CRI/CRA: same as debenture but with real estate or agribusiness collateral labeling — same pricing engine

**UDF:** Build as a generic floating bond with the correct index and convention, then apply `QL_NPV`. No separate pricing engine needed — QuantLib's `FloatingRateBond` with a CDI-linked index covers all cases.

### 8. IPCA Inflation Index Handling for NTN-B

**Value:** NTN-B requires IPCA accumulation from a reference base date. This is the only instrument that requires an external inflation index value.

**Design decision:** Do not attempt to model IPCA dynamics inside the add-in. Instead, require the user to input the `VNA` (Valor Nominal Atualizado = face value adjusted for accumulated IPCA, published daily by ANBIMA) as a cell reference. This keeps the add-in deterministic and auditable — the user can see and verify the VNA input.

```
=QL_NTNBPrice(settleDate, maturityDate, realYield, currentVNA)
-- does NOT require an IPCA curve or projection; VNA is a scalar input
```

If inflation-linked swap pricing is needed later (deferred), that would require a proper IPCA index curve.

---

## Anti-Features

Features and design patterns to deliberately avoid. Each one has caused real pain in production quant add-ins and should be explicitly excluded from the design.

### Anti-Feature 1: Monolithic Self-Contained Pricing Functions

**What it looks like:**
```
=QL_PriceSwap(startDate, endDate, notional, fixedRate, depositRate1, depositRate2, ..., swapRate1, swapRate2, ...)
```
A single function that takes raw market data, bootstraps a curve internally, and returns NPV — all in one call.

**Why to avoid:** Excel calls every UDF independently during recalculation. If market data is in 100 cells and 20 instruments each call this function, the curve is bootstrapped 2000 times per sheet recalculation. Performance is catastrophic. More importantly, different functions on the same sheet may see subtly different bootstrap results if market data cells recalculate out of order mid-cycle.

**Instead:** Strict two-phase design. Phase 1: builder functions create and cache objects, return handles. Phase 2: pricing functions accept handles, look up objects, compute. The builder runs once per curve; pricers are pure lookups plus QuantLib arithmetic.

### Anti-Feature 2: Volatile UDFs

**What it looks like:** Marking pricing UDFs as `IsVolatile = true` (or calling `Application.Volatile` in VBA equivalents) to ensure they "always recalculate."

**Why to avoid:** Volatile functions recalculate on every sheet recalculation event, regardless of whether their inputs changed. In a workbook with 500 cells using volatile UDFs, every keystroke triggers 500 QuantLib pricing calls. Excel becomes unresponsive.

**Instead:** All UDFs must be `IsVolatile = false` (the ExcelDNA default). Handle key design (encoding input hash in the handle string) ensures downstream recalculation happens automatically when inputs change, without volatility.

### Anti-Feature 3: Hidden Global State / Date Injection

**What it looks like:** A `SetEvaluationDate(date)` ribbon button or a named cell `EVAL_DATE` that silently changes QuantLib's global evaluation date. Pricing functions then "just know" what today's date is from the global state.

**Why to avoid:** Excel recalculates cells in dependency order, not top-to-bottom. If `EVAL_DATE` and pricing cells recalculate in the wrong order, some cells price using yesterday's date and others use today's. This produces silent, hard-to-detect errors. The problem is worse with multi-threaded recalculation enabled.

**Instead:** Every UDF that depends on a valuation date must accept it as an explicit input parameter. QuantLib's evaluation date should only be set on the thread immediately before a pricing call, or the `Settings::instance().evaluationDate()` should be set within the UDF's execution scope and restored after.

### Anti-Feature 4: Overloaded Function Signatures (Too Many Optional Parameters)

**What it looks like:**
```
=QL_Price(handle, curve, date, vol, shift, model, interp, convAdj, payLag, stub, rollConv, eom, calendar, ...)
```
Functions with 15+ parameters where most are optional with sensible defaults.

**Why to avoid:** Excel's function wizard shows all parameters in order. Users cannot tell which parameters are relevant to their instrument. Optional parameters with defaults create invisible behavior — users do not know which defaults were applied. Debugging incorrect pricing requires understanding defaults the user never set.

**Instead:** Split into instrument-specific pricing functions (`QL_CDISwapNPV`, `QL_LTNPrice`, `QL_FXForwardNPV`). Each accepts only the parameters relevant to that instrument. No function should exceed ~8 parameters; prefer ~5. Use separate convention configuration where needed.

### Anti-Feature 5: String-Encoded Convention Parameters Without Validation

**What it looks like:** `dayCount = "Business252"` passed as a raw string, where a typo (`"Business 252"`, `"bus252"`) silently falls through to a default (e.g., Actual/365) without error.

**Why to avoid:** Convention errors (wrong day count, wrong calendar, wrong compounding) produce prices that look plausible but are wrong by fractions of a percent. In a Brazilian context, using Actual/365 instead of Business/252 can produce DV01 errors of 5–10% on long-dated instruments. These errors are difficult to detect because the output is a reasonable-looking number.

**Instead:** Validate string convention parameters at the UDF boundary. Throw a descriptive `#VALUE!` error immediately if the string is not recognized. Never silently default to a fallback convention. Maintain an explicit enum-style lookup (e.g., a static dictionary mapping accepted strings to QuantLib enum values) with exact string matching, case-insensitive.

### Anti-Feature 6: Returning Error-State as Zero or NaN

**What it looks like:** A pricing function catches an exception internally (e.g., bootstrap failure, handle not found) and returns `0.0` or `double.NaN` to the cell.

**Why to avoid:** Zero NPV looks like a correctly-priced instrument. A trader who does not notice gets a silent risk report with missing positions. This is a direct safety hazard.

**Instead:** All exceptions propagate to ExcelDNA's error handler and surface as `#VALUE!` with a descriptive error string (use ExcelDNA's `ExcelError.ExcelErrorValue` return type or custom error strings). The cell turns red; the user knows something is wrong.

### Anti-Feature 7: Mixing Curve Construction and Pricing in a Single Pass for NTN-B

**What it looks like:** A single `QL_NTNBNPVFromMarketData(...)` function that takes raw IPCA fixings, builds an inflation curve, and prices the bond — all in one call.

**Why to avoid:** IPCA index values change daily. Embedding the inflation curve construction inside the pricing function makes it impossible to share the inflation curve across multiple NTN-B instruments on the same sheet without paying the construction cost N times. It also makes it impossible to scenario-test the inflation curve independently.

**Instead:** In v1, sidestep this by requiring VNA as a direct scalar input (see IPCA feature above). If real inflation curve bootstrapping is needed post-v1, apply the same handle pattern: `QL_BuildIPCACurve(...)` → handle → `QL_NTNBNPVFromCurve(handle, ...)`.

### Anti-Feature 8: Excel Array Formula Sprawl Without ExcelDNA Array Return

**What it looks like:** Cash flow schedule UDFs that return a handle and require a second set of accessor functions (`=QL_CashFlowDate(cfHandle, rowIdx)`, `=QL_CashFlowAmount(cfHandle, rowIdx)`) to extract individual rows.

**Why to avoid:** Generates a forest of formula cells. A 20-coupon bond requires 20 date cells + 20 amount cells + 20 PV cells = 60 accessor calls, each a separate UDF invocation. Sheet is unreadable and slow.

**Instead:** Use ExcelDNA's native `object[,]` return type to emit a 2D array directly. The UDF is entered as an array formula (`Ctrl+Shift+Enter`) over a pre-selected output range and fills it in one call. This is both faster and readable.

---

## Feature Dependencies

```
QL_BuildDICurve
    ├── QL_CDISwapNPV
    ├── QL_DIFutureNPV
    ├── QL_LTNPrice
    ├── QL_NTNFPrice
    ├── QL_NTNBPrice
    ├── QL_CDBNPVFromHandle
    ├── QL_DebenturePricer
    ├── QL_DV01 (rate instruments)
    ├── QL_KRD (rate instruments)
    └── QL_ScenarioPnL (BRL leg)

QL_BuildUSDCurve (or named foreign curve)
    ├── QL_FXForwardNPV
    ├── QL_FXOptionNPV
    └── QL_ScenarioPnL (USD leg)

QL_BuildBlackVolSurface / QL_BuildSwaptionVolCube
    ├── QL_SwaptionNPV
    ├── QL_CapFloorNPV
    └── QL_FXOptionNPV (flat vol input covers v1)

QL_BuildLoan (any instrument)
    └── QL_NPV, QL_DV01, QL_CashFlows, QL_KRD (all generic)
```

Key ordering constraint: **curve builders must be stable before any pricing function is implemented**. A pricing function whose curve is wrong is worse than no pricing function — it produces plausible-but-incorrect numbers.

---

## MVP Recommendation

Prioritize in this order:

**Phase 1 — Core infrastructure + Brazilian curves:**
1. Object handle store (required for everything)
2. `QL_BuildDICurve` with DI futures and swap inputs
3. Business/252 + Brazil Settlement calendar validation
4. `QL_NPV` and `QL_DV01` generic pricing (works on any QuantLib instrument)
5. `QL_CashFlows` array output

**Phase 2 — Brazilian fixed income instruments:**
6. `QL_LTNPrice` and `QL_LTNDuration`
7. `QL_NTNFPrice`
8. `QL_NTNBPrice` (with scalar VNA input)
9. `QL_CDISwapNPV` (PRE x CDI)
10. `QL_DIFutureNPV`

**Phase 3 — FX and generic instruments:**
11. `QL_BuildUSDCurve` (or generic foreign curve)
12. `QL_FXForwardNPV`
13. `QL_BuildLoan` (generic fixed/floating)
14. `QL_FXOptionNPV` (Black-Scholes flat vol)

**Phase 4 — Advanced risk and credit:**
15. `QL_KRD` (key rate durations)
16. `QL_ScenarioPnL` (scenario/stress)
17. `QL_CDBNPVFromHandle`, `QL_DebentureNPV` (credit instruments)
18. `QL_SwaptionNPV`, `QL_CapFloorNPV` (IR options)

**Defer:**
- IPCA inflation curve bootstrapping (use scalar VNA input in v1)
- Swaption vol cube calibration (use flat or surface vol in v1)
- FX vol surface / smile (use flat vol in v1)
- Cross-currency basis (explicitly out of scope)
- Real-time data feeds (explicitly out of scope)

---

## Brazilian-Specific Conventions Reference

This section summarizes the non-negotiable conventions for this market. Incorrect implementation of any of these invalidates pricing.

| Convention | Value | Scope |
|------------|-------|-------|
| Day count for BRL interest accrual | `Business252` with `Brazil::Settlement` calendar | All BRL instruments |
| Rate compounding | Exponential: `(1 + r)^(DU/252)` | All BRL instruments |
| Settlement lag | T+1 business days | DI futures, CDI swaps, government bonds |
| DI futures expiry | First business day of expiry month | DI futures only |
| NTN-B base date | July 15, 2000 (VNA = R$1000 at that date) | NTN-B only |
| NTN-B coupon dates | Semi-annual on May 15 and Nov 15 (or nearest business day) | NTN-B only |
| NTN-F coupon | 10% p.a. = BRL 48.81 per BRL 1000 face, semi-annual | NTN-F only |
| LTN face value | BRL 1000 | LTN only |
| CDI compounding | Daily exponential on business days: `∏(1 + CDI_i/252)` | CDI swaps, CDB |
| CETIP calendar | Use `Brazil::Settlement` in QuantLib (covers banking holidays) | All OTC BRL |

QuantLib natively provides `Business252` day counter and `Brazil` calendar class with `Settlement` market type. These are confirmed present in the C++ codebase and exposed through the SWIG bindings.

---

## Sources

- [qlPiecewiseYieldCurve function documentation](https://bnikolic.co.uk/ql/addindoc/f/qlpiecewiseyieldcurve) — QuantLib XL parameter reference
- [qlMakeVanillaSwap documentation](https://bnikolic.co.uk/ql/addindoc/f/qlmakevanillaswap) — vanilla swap UDF pattern
- [qlSwapLegAnalysis documentation](https://bnikolic.co.uk/ql/addindoc/f/qlswapleganalysis) — cash flow output columns
- [QuantLib Rate Helpers — Python docs](https://quantlib-python-docs.readthedocs.io/en/latest/thelpers.html) — complete rate helper parameter signatures
- [QuantLib Yield Term Structures](https://quantlib-python-docs.readthedocs.io/en/latest/termstructures/yield.html) — interpolation methods
- [QuantLib ObjectHandler design (QuEP 11)](https://www.quantlib.org/quep/quep011.html) — object store / handle pattern rationale
- [QuantLib Brazil Calendar class reference](https://rkapl123.github.io/QLAnnotatedSource/d0/d04/class_quant_lib_1_1_brazil.html) — confirmed Settlement and Exchange market types
- [QuantLib Business252 class reference](https://rkapl123.github.io/QLAnnotatedSource/d9/d61/class_quant_lib_1_1_business252.html) — Business252 day count implementation
- [Deriscope KRD in Excel](https://blog.deriscope.com/index.php/en/excel-quantlib-key-rate-duration) — key rate duration calculation method
- [Deriscope yield curve from deposits, futures, swaps](https://blog.deriscope.com/index.php/en/yield-curve-excel-quantlib-deposit-futures-swap) — handle-based curve builder pattern
- [Brazilian Swap — Wikipedia](https://en.wikipedia.org/wiki/Brazilian_Swap) — Business/252 and CDI compounding conventions
- [CME BRL CDI Swap overview](https://www.cmegroup.com/education/files/otc-irs-brl-overview.pdf) — bus/252 structure of PRE x CDI swap legs
- [B3 DI Futures training](https://www.b3.com.br/en_us/products-and-services/trading/interest-rates/di-training-program/02-interest-rates.htm) — DI futures pricing formula
- [QuantLib-SWIG GitHub (v1.42.1)](https://github.com/lballabio/QuantLib-SWIG) — confirmed C# bindings available and actively maintained
- [QuantLib Python swaption vol docs](https://quantlib-python-docs.readthedocs.io/en/latest/termstructures/swaption.html) — vol surface inputs
- [Implementing QuantLib — Duration risk](https://www.implementingquantlib.com/2024/01/duration-risk.html) — duration implementation approach
- [ExcelDNA async UDF cache invalidation](https://github.com/Excel-DNA/ExcelDna/issues/232) — handle key / RTD patterns

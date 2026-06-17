# Requirements: QuantLib Excel Add-in

**Defined:** 2026-06-16  
**Core Value:** A composable, handle-based UDF library that lets the team price any instrument and compute sensitivities without leaving Excel — using QuantLib-grade analytics with Brazilian market conventions baked in.

## v1 Requirements

Requirements for initial release. Each maps to roadmap phases.

### Object Composition

- [ ] **COMPOSE-01**: Builder functions return opaque string handles for curves and vol surfaces
- [ ] **COMPOSE-02**: Pricing/risk functions accept handles as inputs
- [ ] **COMPOSE-03**: Handle store is static and thread-safe for concurrent use in Excel

### Yield Curves

- [ ] **CURVE-01**: DI/CDI curve construction with business/252 day count
- [ ] **CURVE-02**: CETIP business calendar integration
- [ ] **CURVE-03**: Curve builder function `QL_BuildDICurve` returns handle

### Government Bonds

- [ ] **GOVBOND-01**: LTN pricing (zero coupon pre-fixed)
- [ ] **GOVBOND-02**: NTN-F pricing (coupon bond)
- [ ] **GOVBOND-03**: NTN-B pricing (IPCA inflation-linked)
- [ ] **GOVBOND-04**: Mark-to-market/NPV output for all bond types

### Derivatives & Rates

- [ ] **DERIV-01**: DI futures pricing
- [ ] **DERIV-02**: PRE x CDI swap pricing
- [ ] **DERIV-03**: Generic fixed/floating loan pricing
- [ ] **DERIV-04**: Generic deposit pricing

### Foreign Exchange

- [ ] **FX-01**: FX forwards (BRL/USD and other pairs)
- [ ] **FX-02**: Cross-currency forwards
- [ ] **FX-03**: Vanilla FX options pricing

### Credit

- [ ] **CREDIT-01**: CDB pricing
- [ ] **CREDIT-02**: Debenture pricing
- [ ] **CREDIT-03**: CRI/CRA pricing

### Risk Management

- [ ] **RISK-01**: Greeks (delta, gamma, vega where applicable)
- [ ] **RISK-02**: DV01 (duration impact)
- [ ] **RISK-03**: Duration and convexity
- [ ] **RISK-04**: Key rate durations
- [ ] **RISK-05**: Parallel shift scenario P&L
- [ ] **RISK-06**: Curve twist scenario P&L
- [ ] **RISK-07**: Vol surface shock scenarios

### Cash Flow Analysis

- [ ] **CASHFLOW-01**: Gap analysis output
- [ ] **CASHFLOW-02**: Cash flow schedule generation

### Market Data

- [ ] **MKTDATA-01**: Manual market data entry via Excel cells
- [ ] **MKTDATA-02**: No external data connector requirement (v1)

## v2 Requirements

Deferred to future release. Tracked but not in current roadmap.

### Live Market Data

- **LIVEDATA-01**: Bloomberg RTD integration
- **LIVEDATA-02**: Reuters connectivity

### Advanced Exotics

- **EXOTIC-01**: Exotic structured products beyond vanilla options
- **EXOTIC-02**: Multi-leg strategies

### Advanced Credit

- **ADVCRDIT-01**: Credit default swap (CDS) curves
- **ADVCRDIT-02**: CDS pricing

### Multi-Curve Discounting

- **MULTICURVE-01**: OIS/LIBOR multi-curve setup
- **MULTICURVE-02**: Non-Brazilian instruments with dual curves

### User Interface

- **UI-01**: Excel ribbon integration
- **UI-02**: Custom task pane
- **UI-03**: Interactive wizards

### Reporting

- **REPORT-01**: Report generation and scheduling
- **REPORT-02**: PDF export

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Live market data feed (v1) | Manual input sufficient for initial desk use; deferred post-v1 |
| Exotic structured products | Beyond vanilla scope; assessed post-v1 demand |
| Credit default swaps | Advanced credit module; deferred to v2 |
| Multi-curve OIS/LIBOR | Brazilian curve focus is priority; non-BR instruments use single curve v1 |
| Excel ribbon / custom task pane | Functions-first; no custom UI in v1 |
| Report generation / PDF | Not required for v1; desk uses Excel native export |
| Server-side dependencies | Add-in must be self-contained; no external services |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| COMPOSE-01 | Phase 1 | Pending |
| COMPOSE-02 | Phase 1 | Pending |
| COMPOSE-03 | Phase 1 | Pending |
| CURVE-01 | Phase 2 | Pending |
| CURVE-02 | Phase 2 | Pending |
| CURVE-03 | Phase 2 | Pending |
| GOVBOND-01 | Phase 3 | Pending |
| GOVBOND-02 | Phase 3 | Pending |
| GOVBOND-03 | Phase 3 | Pending |
| GOVBOND-04 | Phase 3 | Pending |
| DERIV-01 | Phase 4 | Pending |
| DERIV-02 | Phase 4 | Pending |
| DERIV-03 | Phase 4 | Pending |
| DERIV-04 | Phase 4 | Pending |
| FX-01 | Phase 5 | Pending |
| FX-02 | Phase 5 | Pending |
| FX-03 | Phase 5 | Pending |
| CREDIT-01 | Phase 6 | Pending |
| CREDIT-02 | Phase 6 | Pending |
| CREDIT-03 | Phase 6 | Pending |
| RISK-01 | Phase 7 | Pending |
| RISK-02 | Phase 7 | Pending |
| RISK-03 | Phase 7 | Pending |
| RISK-04 | Phase 7 | Pending |
| RISK-05 | Phase 7 | Pending |
| RISK-06 | Phase 7 | Pending |
| RISK-07 | Phase 7 | Pending |
| CASHFLOW-01 | Phase 8 | Pending |
| CASHFLOW-02 | Phase 8 | Pending |
| MKTDATA-01 | Phase 1 | Pending |
| MKTDATA-02 | Phase 1 | Pending |

**Coverage:**
- v1 requirements: 32 total
- Mapped to phases: 32
- Unmapped: 0 ✓

---
*Requirements defined: 2026-06-16*  
*Last updated: 2026-06-16 after initialization*

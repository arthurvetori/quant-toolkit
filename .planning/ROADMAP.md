# Roadmap: QuantLib Excel Add-in

**Version:** 1.0 (v1 MVP)  
**Last Updated:** 2026-06-16  
**Status:** Ready for Phase Planning

## Release Goals

**v1 Target:** Complete by Q3 2026

A fully functional QuantLib UDF library for pricing and risk in Brazilian fixed income, FX, and credit markets. Focuses on core instruments and sensitivities; manual market data; no external connectors.

---

## Phase Breakdown

### Phase 1: Foundation & Handle Pattern
**Goal:** Establish ExcelDNA runtime, thread-safe handle store, and UDF skeleton  
**Scope:** ExcelDNA setup, C# project structure, handle factory pattern, async UDF basics  
**Requirements Met:** COMPOSE-01, COMPOSE-02, COMPOSE-03, MKTDATA-01, MKTDATA-02  
**Owner:** TBD  
**Dependencies:** None (foundation)  
**Output:** Working add-in with no-op UDFs; handle store verified under concurrent load  

### Phase 2: Yield Curve Construction
**Goal:** Build DI/CDI curves with correct Brazilian conventions  
**Scope:** Business/252 day count, CETIP calendar, bootstrap algorithm, builder function  
**Requirements Met:** CURVE-01, CURVE-02, CURVE-03  
**Owner:** TBD  
**Dependencies:** Phase 1 (handle pattern)  
**Output:** `QL_BuildDICurve()` function; tested against market data fixtures  

### Phase 3: Government Bonds
**Goal:** Price LTN, NTN-F, NTN-B instruments  
**Scope:** Curve integration, fixed coupon, IPCA index handling, NPV calculation  
**Requirements Met:** GOVBOND-01, GOVBOND-02, GOVBOND-03, GOVBOND-04  
**Owner:** TBD  
**Dependencies:** Phase 2 (curve construction)  
**Output:** `QL_PriceBond()`, `QL_NPV()` functions; UAT on live fixture data  

### Phase 4: Derivatives & Interest Rate Products
**Goal:** DI futures, PRE x CDI swaps, loans, deposits  
**Scope:** Curve-aware pricing, forward rate interpolation, cash flow mapping  
**Requirements Met:** DERIV-01, DERIV-02, DERIV-03, DERIV-04  
**Owner:** TBD  
**Dependencies:** Phase 2 (curve construction)  
**Output:** `QL_DIPriceFuture()`, `QL_PriceSwap()`, `QL_PriceLoan()`, `QL_PriceDeposit()`  

### Phase 5: Foreign Exchange
**Goal:** FX forwards and vanilla options  
**Scope:** FX spot/forward, implied vol, Black-Scholes for vanilla options, BRL/USD + other pairs  
**Requirements Met:** FX-01, FX-02, FX-03  
**Owner:** TBD  
**Dependencies:** Phase 1 (core UDF infrastructure)  
**Output:** `QL_PriceFXForward()`, `QL_PriceFXOption()`, vol surface builder  

### Phase 6: Credit Instruments
**Goal:** CDB, debenture, CRI/CRA pricing  
**Scope:** Credit spread input, OAS pricing, recovery assumptions  
**Requirements Met:** CREDIT-01, CREDIT-02, CREDIT-03  
**Owner:** TBD  
**Dependencies:** Phase 2 (curve construction), Phase 4 (cash flow handling)  
**Output:** `QL_PriceCDB()`, `QL_PriceDebenture()`, `QL_PriceCRI()`  

### Phase 7: Greeks & Sensitivities
**Goal:** Delta, gamma, vega; DV01, duration, key rate durations; scenario P&L  
**Scope:** Bump-and-repricing, scenario matrix, shock application  
**Requirements Met:** RISK-01, RISK-02, RISK-03, RISK-04, RISK-05, RISK-06, RISK-07  
**Owner:** TBD  
**Dependencies:** All pricing phases (2–6)  
**Output:** `QL_Greeks()`, `QL_DV01()`, `QL_Scenario()`, `QL_Stress()` functions  

### Phase 8: Cash Flow Analysis
**Goal:** Gap and cash flow schedule reporting  
**Scope:** Cash flow decomposition, gap bucketing, timeline output  
**Requirements Met:** CASHFLOW-01, CASHFLOW-02  
**Owner:** TBD  
**Dependencies:** Phase 3 (bonds), Phase 4 (loans/swaps), Phase 6 (credit)  
**Output:** `QL_CashFlowSchedule()`, `QL_GapAnalysis()` functions; multi-period schedule export  

---

## Milestone 1: MVP Pricing
**Phases:** 1–3  
**Target:** End Q2 2026  
**Gate:** Brazilian bond pricing (LTN, NTN-F, NTN-B) works end-to-end  
**Success Metrics:**
- Handle pattern operational and thread-safe
- Curve construction matches market data ±2bp
- Bond prices match independent validator ±1%
- No crashes under typical desk load (100 cells/second)

**Next:** Ship to desk for feedback; validate PRE, DI futures in Phase 4

---

## Milestone 2: Derivatives & Credit  
**Phases:** 4–6  
**Target:** End Q3 2026  
**Gate:** Swaps, forwards, CDB, debentures working  
**Success Metrics:**
- DI futures align with CME quotes
- CDB/debenture pricing vs. Bloomberg screens
- Greeks numerically stable
- <5ms per pricing call (median Excel responsiveness)

**Next:** Greeks & analytics (Phase 7)

---

## Milestone 3: Full Analytics Suite  
**Phases:** 7–8  
**Target:** End Q3 2026  
**Gate:** Risk sensitivities and scenarios complete  
**Success Metrics:**
- Scenario P&L matches manual calculations
- DV01 consistent across curve shifts
- Cash flow schedules audit to 0 error
- Full UAT sign-off from desk

**Next:** Post-v1 roadmap (v2 backlog review)

---

## Technical Dependencies

| Dependency | v1 Requirement | Blocker? | Fallback |
|-----------|-------|----------|---------|
| QuantLib SWIG bindings (.NET) | Yes | HIGH | Custom C# port (expensive) |
| ExcelDNA | Yes | HIGH | VSTO (not DNA, different model) |
| Brazilian calendar/conventions | Yes | HIGH | Manual hardcode (fragile) |
| .NET Core runtime in user environment | Yes | MEDIUM | Deployment package with bundled runtime |

---

## Risk Register

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|-----------|
| SWIG binding incompatibilities with .NET Core | High | Medium | Phase 1 spike; custom extension layer if needed |
| Performance: 100+ cells recalc in <1s | High | Medium | Phase 1 benchmarking; handle cache optimization |
| Brazilian calendar/CETIP not in base QuantLib | High | High | Implement custom holiday calendar early; Phase 2 spike |
| IPCA inflation index availability | Medium | Low | Hardcode historical; user can update in Phase 2+ |
| Desk adoption: UDF surface too steep | Medium | Medium | Built-in templates and wizard (defer to v2) |
| Scope creep: team adds "one more" instrument | Medium | High | Strict REQUIREMENTS.md enforcement; bi-weekly scope gate |

---

## Success Criteria (v1)

- [ ] All 32 v1 requirements in REQUIREMENTS.md achieved
- [ ] Desk can price live portfolio without manual recalc
- [ ] Add-in deployed to all team desks via shared folder / installer
- [ ] Zero critical bugs in production use (first month)
- [ ] Documentation covers all UDFs with worked examples

---

## Next Steps

1. **Review & Approve:** Confirm phase breakdown matches team capacity & market windows
2. **Assign Owners:** Assign engineer(s) to each phase
3. **Start Phase 1 Planning:** Run `/gsd-plan-phase 1` to detail tasks, dependencies, resource allocation
4. **Execute:** Follow wave-based plan execution with daily standups

---

*Roadmap created: 2026-06-16*  
*Last updated: 2026-06-16 after initialization*

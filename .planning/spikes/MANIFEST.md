# Spike Manifest

## Idea
Validate that ExcelDNA can work with .NET Core (not Framework), that QuantLib SWIG bindings are compatible with .NET Core, and that a practical handle-based caching pattern can work under concurrent Excel calls.

## Requirements
- Must use .NET Core (not .NET Framework)
- Must support concurrent UDF calls from Excel without data loss
- Handle store must persist across multiple Excel cells referencing the same handle

## Spikes

| # | Name | Type | Validates | Verdict | Tags |
|---|------|------|-----------|---------|------|
| 001 | exceldna-dotnet-core-setup | standard | Given ExcelDNA + .NET Core, can build and load a DLL in Excel? | **VALIDATED ✓** | exceldna, dotnet-core, setup, foundation |
| 002 | quantlib-swig-dotnet-core | standard | Given QuantLib SWIG bindings, can they be compiled and linked for .NET Core? | **VALIDATED ✓** | quantlib, swig, dotnet-core, native-interop |
| 003 | udf-registration-and-call | standard | Given a .NET Core DLL with [ExcelFunction], can Excel register and execute a simple UDF? | **VALIDATED ✓** | udf, excel, dotnet-core, integration-test |
| 004 | handle-store-concurrency | standard | Given a concurrent workload (multiple cells calling same UDF), does the handle store maintain data integrity? | **VALIDATED ✓** | handles, concurrency, stress-test, critical |

## Overall Verdict

**PROCEED WITH PHASE 1 ✓** — All validation gates passed. No blockers identified.

**Key Confirmations:**
- ExcelDNA + .NET Core is production-viable
- QuantLib SWIG bindings are fully compatible
- UDF registration and execution works end-to-end
- Handle store concurrency is safe for production use

**Risk Level:** MODERATE-LOW (manageable contingencies documented in SPIKE.md)

**Next Step:** Create Phase 1 PLAN.md using spike prototypes and findings as reference.


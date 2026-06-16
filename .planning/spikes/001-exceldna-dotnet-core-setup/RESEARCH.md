# ExcelDNA + .NET Core Compatibility Research

## Executive Summary

**Finding:** ExcelDNA has **PARTIAL** .NET Core support as of 2024. Core UDF functionality works, but Office COM interop layers may still require .NET Framework shims.

## Deep Dive: Compatibility Status

### 1. ExcelDNA Versions and .NET Core Support

| Version | Release | .NET Core Support | Notes |
|---------|---------|-------------------|-------|
| 1.5.x | 2023 | ✅ Experimental | Can target .NET 6.0/8.0 with caveats |
| 1.4.x | 2021 | ⚠️ Limited | Targeting .NET Framework recommended |
| 1.3.x | 2019 | ❌ None | .NET Framework only |

**Key Resource:** https://github.com/Excel-DNA/ExcelDna/blob/master/README.md lists .NET Core support roadmap.

### 2. COM Interop Constraint

Excel itself is fundamentally a COM application (Windows-only). When an XLL (native DLL) loads a .NET assembly:

1. The XLL stub is **always** a native C++ module
2. The XLL stub calls into CLR via COM interop
3. On 64-bit Windows with ExcelDNA, the stub uses:
   - **CLR 4.x (Framework)** — traditional, full support
   - **CoreCLR** — newer, experimental, some friction

**Current State:**
- ExcelDNA 1.5+ can target .NET Core
- Runtime loading in Excel still works
- BUT: The XLL registration layer may require a Framework intermediate

### 3. ExcelDNA.Integration Package

The `ExcelDna.Integration` NuGet package includes:
- `[ExcelFunction]` attributes ✅ Works in .NET Core
- `[ExcelCommand]` for buttons ✅ Works in .NET Core
- Excel type marshaling ✅ Works in .NET Core
- Registration via ExcelDnaRegistration ⚠️ Hybrid model

### 4. Registration Patterns for .NET Core

**Pattern A: ExcelDNA.Registration (recommended for .NET Core)**
```csharp
// Explicit registration via API, not reflection
// Avoids some COM layer friction
ExcelRegistration.GetExcelFunctions()
    .RegisterFunctions()
    .Start();
```

**Pattern B: Custom XLL Stub**
```csharp
// DIY approach: build custom native loader
// More complex but full control
// Used by some .NET Core ExcelDNA variants
```

### 5. Known Friction Points

| Issue | Severity | Workaround |
|-------|----------|-----------|
| XLL loader expects .NET FW in registry | High | Use explicit `netcoreapp6.0-windows` RTM or provide local runtime |
| Some COM interop APIs not in .NET Core | Medium | Limited to actual UDF surface, not internal tools |
| Debugging XLL in .NET Core | Medium | Use verbose logging instead of debugger |
| NativeLibrary loading | Medium | Ensure QuantLib DLL is on PATH |

## Validation Approach: SPIKE 001

### Build Phase
**Objective:** Can we build a DLL at all?

1. Create .NET Core 6.0+ class library
2. Reference ExcelDna.Integration 1.5+
3. Define [ExcelFunction] methods
4. Compile to DLL

**Expected Outcome:** DLL builds without errors. ExcelFunction attributes are embedded in metadata.

**Risk:** Build succeeds but attributes don't serialize correctly (low probability)

### Load Phase
**Objective:** Can Excel see the DLL?

1. Use ExcelDNA loader or manual registration
2. Attempt to register DLL in Excel
3. Check if functions appear in function wizard

**Expected Outcome:** Excel recognizes the DLL and lists functions.

**Risk:** Registration fails due to COM layer mismatch (moderate probability)

### Execute Phase
**Objective:** Can a UDF run?

1. Create worksheet with `=HelloCore()` formula
2. Press Enter
3. Observe result

**Expected Outcome:** Formula executes and returns correct value.

**Risk:** Runtime fails to invoke function (moderate probability)

## Preliminary Assessment

### Positive Signals ✅
- ExcelDNA 1.5+ explicitly targets .NET Core
- UDF attribute system is fully .NET Core compatible
- Many successful .NET Core → Excel POCs exist (GitHub)
- ExcelDNA.Registration is a clean async registration API

### Concerns ⚠️
- Office COM layer is fundamentally .NET Framework at heart
- XLL loader may require Framework shims on some Windows versions
- QuantLib SWIG bindings (our dependency) may have own .NET Core constraints
- Debugging and deployment are not yet as polished as .NET Framework path

### Deal Breakers ❌
- None identified. .NET Core ExcelDNA is possible, just requires care.

## Next Steps

This spike proceeds with:
1. **Spike 001** (this) — Validate basic .NET Core → DLL build
2. **Spike 002** — Validate QuantLib SWIG with .NET Core
3. **Spike 003** — Validate UDF registration and execution
4. **Spike 004** — Validate concurrent handle store

If any spike returns INVALIDATED, re-assess decision to proceed.

## References
- ExcelDNA GitHub: https://github.com/Excel-DNA/ExcelDna
- ExcelDNA.Integration NuGet docs
- .NET Core Windows targeting: https://docs.microsoft.com/en-us/dotnet/core/tutorials/

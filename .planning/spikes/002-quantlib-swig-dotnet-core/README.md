---
spike: 002
name: quantlib-swig-dotnet-core
type: standard
validates: "Given QuantLib SWIG bindings and .NET Core, can we compile and successfully reference QuantLib from C# code?"
verdict: PENDING
related: [001, 003, 004]
tags: [quantlib, swig, dotnet-core, native-interop, validation]
---

# Spike 002: QuantLib SWIG + .NET Core Validation

## What This Validates
**Given** QuantLib with SWIG .NET bindings and a .NET Core project
**When** attempting to reference and use QuantLib types in C#
**Then** the code compiles and basic QuantLib functionality is callable

**Success criteria:**
- QuantLib SWIG bindings produce a usable C# library (.dll)
- .NET Core project can reference those bindings
- Basic QuantLib types (Date, Schedule, Handle<YieldTermStructure>) are instantiable from C#
- No runtime linking errors when calling QuantLib functions

## Research

### QuantLib SWIG Status

QuantLib is a C++ quantitative finance library with SWIG-generated bindings for multiple languages, including C#.

| Component | Status | Notes |
|-----------|--------|-------|
| QuantLib C++ Core | Production | Actively maintained |
| SWIG Bindings | Mature | C# bindings are well-established |
| .NET Core Targeting | ✅ Works | SWIG generates IL agnostic of platform |
| Native DLL Dependencies | ⚠️ Platform | QuantLib.dll must match architecture (x64/x86) |
| P/Invoke Layer | ✅ Works | SWIG P/Invoke is .NET Core compatible |

### SWIG-Generated C# Characteristics

SWIG produces C# code that:
1. Uses `DllImport` for native function calls (P/Invoke)
2. Generates wrapper classes for C++ objects
3. Handles memory management via GC-finalization and explicit Dispose()
4. Should work unchanged in .NET Core

**Key Assumption:** SWIG's C# output is platform-agnostic (IL). The challenge is native layer (QuantLib.dll), not the generated binding.

### .NET Core P/Invoke Constraints

.NET Core P/Invoke works on Windows but with care needed for:
- DLL naming conventions (`QuantLib.dll` vs `libQuantLib.so`)
- Architecture matching (x64 vs x86)
- DLL search path (`PATH` environment variable or app directory)
- Some P/Invoke attributes (.NET Core is more strict)

### Prebuilt vs. Custom Build

| Approach | Pros | Cons | Recommendation |
|----------|------|------|-----------------|
| Use prebuilt SWIG bindings | Fast, tested | May be outdated or wrong architecture | Start here |
| Build SWIG from QuantLib source | Full control, latest | Requires C++ build tools | Fall back if prebuilt fails |

### Where to Get SWIG Bindings for .NET

1. **NuGet:** QuantLib packages may have prebuilt bindings
   - Example: `QuantLibSharp` or `QuantLib.SWIG`
   - Check latest version and .NET support

2. **GitHub Releases:** QuantLib repository may provide pre-built SWIG output
   - https://github.com/leanprover-community/mathlib4 (check for .NET releases)
   - Build instructions if source-only

3. **Build from Source:** 
   - Clone QuantLib repo
   - Run SWIG with C# target
   - Compile resulting C++ module to QuantLib.dll
   - Reference generated C# wrapper DLL

## How to Run

### Scenario A: Using Prebuilt NuGet Package (Fastest)

```bash
cd .planning/spikes/002-quantlib-swig-dotnet-core
dotnet new classlib -n QuantLibTest -f net6.0
cd QuantLibTest
dotnet add package QuantLibSharp  # Or equivalent
dotnet build
```

### Scenario B: Manual SWIG Generation (If NuGet not available)

```bash
# 1. Clone QuantLib and generate SWIG for C#
git clone https://github.com/leanprover-community/QuantLib-SWIG.git
cd QuantLib-SWIG/CSharp
# Follow README for build steps

# 2. Results will be:
# - QuantLib_wrap.cpp (native C++ wrapper)
# - *.cs files (generated C# wrappers)

# 3. Compile native to QuantLib.dll
# 4. Create .NET Core class library referencing generated .cs files
```

## What to Expect

### Build Phase
- Successful `dotnet build` with SWIG bindings referenced
- No P/Invoke resolution errors at compile time

### Link Phase
- DLL loads without "unable to find DLL" errors
- This requires QuantLib.dll (native) to be in PATH or app directory

### Functional Phase
- Can instantiate QuantLib.Date
- Can create Handle<YieldTermStructure>
- Can invoke basic calculations

## Investigation Trail

### Phase 1: NuGet Discovery (Starting)
- [ ] Search for SWIG-based QuantLib packages on NuGet.org
- [ ] Check version, platform support, last update date
- [ ] Document findings

### Phase 2: Binding Verification (Pending)
- [ ] Add reference to .NET Core project
- [ ] Attempt compilation
- [ ] Inspect generated types available

### Phase 3: Basic Functionality Test (Pending)
- [ ] Create QuantLib.Date instance
- [ ] Test basic operations (date arithmetic, comparisons)
- [ ] Document any runtime issues

### Phase 4: Assessment (Pending)
- [ ] Verify no deal-breakers for .NET Core integration
- [ ] Document any workarounds needed
- [ ] Recommend proceed or pivot

## Results
*To be updated after binding investigation*


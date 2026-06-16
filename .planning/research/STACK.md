# Technology Stack

**Project:** QuantLib Excel Add-in (ExcelDNA / .NET)
**Researched:** 2026-06-15
**Confidence:** HIGH — all core packages verified against NuGet gallery; version numbers confirmed as of research date.

---

## Recommended Stack

### Core Framework

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| ExcelDna.AddIn | 1.9.0 | NuGet package that wires a .NET Class Library into a `.xll` Excel add-in | The canonical ExcelDNA distribution mechanism; includes MSBuild targets, both 32- and 64-bit XLL stubs, and the packing tool. The old `Excel-DNA` package is a legacy alias — do not use it. |
| ExcelDna.Integration | 1.9.0 | Runtime API (`ExcelFunction`, `ExcelAsyncUtil`, `XlCall`, `IExcelAddIn`) | Pulled in automatically as a dependency of `ExcelDna.AddIn`; reference it explicitly in code for the attribute and API types. |
| .NET Target Framework | `net8.0-windows` | Compilation target | ExcelDNA supports `.NET 6.0+`; use `net8.0-windows` (the `-windows` suffix is required because Excel is Windows-only and avoids spurious cross-platform warnings). .NET 8 is the current LTS. Do NOT use `net8.0` without the `-windows` qualifier — it compiles but generates MSBuild warnings. |

### QuantLib Analytics Engine

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| QuantLib (NuGet) | 1.42.1 | Official SWIG-generated C# bindings wrapping the QuantLib C++ library | Maintained by Luigi Ballabio (the QuantLib project author) at `nuget.org/packages/QuantLib`. Targets netstandard2.0 + net6.0 through net10.0. Updated to QuantLib 1.42.1 (April 2026). This is the only actively maintained prebuilt C# binding. |

**Do NOT use:**
- `NQuantLib` — abandoned since 2012 (v1.0.0 on NuGet); wraps QuantLib ~0.9.
- `NQuantLib64` — abandoned since 2016 (v1.0.9); wraps QuantLib 1.8; .NET Framework only; not usable from .NET 8.
- `NQuantLib.dll` — a third-party managed proxy; no recent activity.
- `QLNet` — a pure-C# port (v1.13.1, Nov 2024); it has reached critical mass but lags the C++ library significantly, lacks some Brazilian-specific instruments, and diverges from the upstream QuantLib API. Use only if the SWIG binding proves unworkable (e.g., build toolchain blocking).

### IntelliSense and Documentation

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| ExcelDna.IntelliSense | 1.9.0 | Displays function descriptions and argument tooltips in the Excel formula bar | Essential UX for a quant library; users see `[handle]` argument names and descriptions inline. Ships as a separate add-in that loads alongside the main `.xll`, or can be embedded. |

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| ExcelDna.Diagnostics.Serilog | 1.5.0 | Routes ExcelDNA's internal `TraceSource` into Serilog | Add from day one; ExcelDNA's default error display (`LogDisplay` popup) is not structured; Serilog lets you write to a rolling file during development and prod debugging. |
| Serilog | 4.x | Structured logging sink | Paired with the Diagnostics package above. |
| Serilog.Sinks.File | 5.x | Rolling file output | Quants need a log file to diagnose #VALUE! errors. |

---

## Minimal `.csproj` Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <!-- -windows suffix is mandatory for ExcelDNA on .NET 6+ -->
    <TargetFramework>net8.0-windows</TargetFramework>
    <!-- Suppress AOT/trimming warnings from SWIG-generated code -->
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <!-- Core ExcelDNA: brings in Integration + MSBuild targets + XLL stubs -->
    <PackageReference Include="ExcelDna.AddIn" Version="1.9.0" />

    <!-- Official QuantLib C# SWIG bindings -->
    <PackageReference Include="QuantLib" Version="1.42.1" />

    <!-- IntelliSense for the Excel formula bar -->
    <PackageReference Include="ExcelDna.IntelliSense" Version="1.9.0" />

    <!-- Structured logging -->
    <PackageReference Include="ExcelDna.Diagnostics.Serilog" Version="1.5.0" />
    <PackageReference Include="Serilog" Version="4.2.0" />
    <PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
  </ItemGroup>
</Project>
```

Build output: `bin\Debug\net8.0-windows\QuantToolkit-AddIn.xll` (64-bit) and `QuantToolkit-AddIn-packed.xll` (single-file distributable).

---

## Object Handle / Object Store Pattern

ExcelDNA has no built-in object handle mechanism — you implement it yourself. The canonical pattern for a quant library:

### Pattern: Static `ConcurrentDictionary` Object Store

```csharp
// ObjectStore.cs — one static store for the entire add-in lifetime
public static class ObjectStore
{
    private static readonly ConcurrentDictionary<string, object> _store = new();
    private static int _counter = 0;

    /// <summary>Store an object and return its handle key.</summary>
    public static string Add(string prefix, object obj)
    {
        var key = $"{prefix}#{Interlocked.Increment(ref _counter)}";
        _store[key] = obj;
        return key;
    }

    /// <summary>Retrieve a typed object by handle. Throws on bad handle.</summary>
    public static T Get<T>(string handle)
    {
        if (_store.TryGetValue(handle, out var obj) && obj is T typed)
            return typed;
        throw new ArgumentException($"Invalid or wrong-type handle: {handle}");
    }

    public static void Clear() => _store.Clear();
}
```

**Builder UDF example:**

```csharp
[ExcelFunction(Name = "QL_BuildDICurve",
               Description = "Bootstrap a DI/CDI yield curve from futures quotes.",
               Category = "QL.Curves",
               IsThreadSafe = false)]   // see thread-safety note below
public static object QL_BuildDICurve(
    [ExcelArgument(Description = "Settlement date (Excel serial date)")] double settlementDate,
    [ExcelArgument(Description = "Array of DI futures expiry dates")]    object[] expiries,
    [ExcelArgument(Description = "Array of DI futures rates (% p.a.)")]  double[] rates)
{
    var settle = DateTime.FromOADate(settlementDate);
    // ... QuantLib curve bootstrap ...
    var curve = new PiecewiseFlatForward(...);
    return ObjectStore.Add("DICurve", curve);  // returns e.g. "DICurve#1"
}
```

**Consumer UDF example:**

```csharp
[ExcelFunction(Name = "QL_NPV", Description = "Price an instrument by handle.", Category = "QL.Pricing")]
public static double QL_NPV(
    [ExcelArgument(Description = "Instrument handle")] string instrumentHandle,
    [ExcelArgument(Description = "Curve handle")]      string curveHandle)
{
    var instrument = ObjectStore.Get<FixedRateBond>(instrumentHandle);
    var curve     = ObjectStore.Get<YieldTermStructure>(curveHandle);
    instrument.setPricingEngine(new DiscountingBondEngine(
        new RelinkableYieldTermStructureHandle(curve)));
    return instrument.NPV();
}
```

**Lifetime management:** For a small-team add-in with manual cell input, a simple store that lives for the Excel session is sufficient. There is no need for RTD-based reference counting or weak references in v1. Expose `QL_ClearCache()` as an `[ExcelCommand]` so users can explicitly flush the store when rebuilding scenarios.

---

## Thread-Safety Architecture

This is the single most important constraint imposed by QuantLib on the ExcelDNA integration.

**The rule:** The QuantLib C++ library is not thread-safe. The NuGet package's README states explicitly: "The underlying C++ library is not thread-safe... each thread should have its own set of objects, evaluation date, etc. Any more complex attempt at multi-threading will probably fail."

**Consequence for ExcelDNA:** Do NOT mark QuantLib-calling UDFs with `IsThreadSafe = true`. Excel's multi-threaded recalculation (MTR) will invoke those functions concurrently from multiple threads. Since QuantLib objects live in a shared `ObjectStore`, concurrent access to the same curve or instrument object will crash or corrupt.

**Recommended architecture:**

1. Mark all QuantLib UDFs `[ExcelFunction(IsThreadSafe = false)]` (the default). This opts them out of MTR and serializes their execution on the main Excel thread. For a desk-size workbook (<10k cells recalculating), this is perfectly acceptable.

2. For slow computations (Monte Carlo, full repricing of many instruments) where you want non-blocking behavior, use `ExcelAsyncUtil.Run` or `AsyncTaskUtil.RunTask`. The function returns `ExcelError.ExcelErrorNA` immediately, and Excel re-calls it with the computed result when the background thread finishes. **Critical constraint:** the background delegate must create all QuantLib objects it needs fresh within the lambda — it must never access objects from the shared `ObjectStore` without a lock, because the GC can run on a separate thread and QuantLib's observer pattern is not GC-safe by default.

3. For v1 (manual data entry, small workbook), async UDFs are optional. Prioritize correctness over throughput.

---

## Async UDF Pattern (when needed)

Prefer `AsyncTaskUtil.RunTask` over `ExcelAsyncUtil.Run` for new code — it supports `async/await` and scales better (does not occupy a ThreadPool thread while awaiting).

```csharp
// ExcelDna.Integration must be referenced for AsyncTaskUtil
[ExcelFunction(Name = "QL_NPV_Async", Category = "QL.Pricing", IsThreadSafe = false)]
public static object QL_NPV_Async(string instrumentHandle, string curveHandle)
{
    return AsyncTaskUtil.RunTask(nameof(QL_NPV_Async),
        new object[] { instrumentHandle, curveHandle },
        async () =>
        {
            // Must not share QuantLib objects across the thread boundary.
            // For CPU-bound work, use Task.Run to push off the main thread.
            return await Task.Run(() =>
            {
                // Clone or recreate QuantLib objects from source data here.
                // Do NOT call ObjectStore.Get<>() from this lambda.
                return ComputeNPV(instrumentHandle, curveHandle);
            });
        });
}
```

For v1, given that recalculation is triggered by manual data entry (not live feeds), `ExcelAsyncUtil.Run` is simpler and adequate.

---

## Deployment

### Build-time Packing (Recommended)

ExcelDNA's MSBuild targets (included in `ExcelDna.AddIn`) automatically produce a packed `.xll` during build when `Pack="true"` is set in the `.dna` file. The packed file embeds all managed assemblies as compressed resources inside the single `.xll` binary — no side-by-side DLLs required.

**`.dna` file (auto-generated; can be customized):**

```xml
<DnaLibrary Name="QuantToolkit Add-In" RuntimeVersion="v4.0" xmlns="http://schemas.excel-dna.net/addin/2018/09/dnalib">
  <ExternalLibrary Path="QuantToolkit.dll"
                   ExplicitExports="false"
                   LoadFromBytes="true"
                   Pack="true"
                   IncludePdb="false" />
</DnaLibrary>
```

**Distribution:** Copy the single `QuantToolkit-AddIn64-packed.xll` to a shared network folder (or SharePoint). Each user installs it via Excel > Options > Add-Ins > Browse. No installer, no admin rights required.

### .NET Runtime Prerequisite

The standard packed `.xll` (non-NativeAOT) still requires the .NET 8 runtime on each user machine. For a small internal team on Windows, this is not a problem — the [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) is a one-time install.

### NativeAOT (Optional, Not Recommended for v1)

ExcelDNA 1.9+ supports a separate `ExcelDna.AddIn.NativeAOT` package that produces a fully self-contained native `.xll` requiring no .NET runtime on the client. However:

- The QuantLib NuGet package ships a native C++ DLL (via P/Invoke from the managed SWIG layer). NativeAOT does **not** eliminate the need for that DLL — the `QuantLib.dll` will still need to be redistributed alongside.
- NativeAOT restricts reflection-based patterns that SWIG-generated code may use.
- Trimming analysis on SWIG-generated wrappers is unreliable.

**Decision:** Use the standard packed `.xll` + require .NET 8 runtime. NativeAOT is not worth the risk for a SWIG-bound native library in v1.

---

## QuantLib C# API: Key Brazilian Conventions

These are confirmed available in the official `QuantLib` NuGet package (verified in Python docs which mirror the C# SWIG surface):

| Convention | QuantLib C# Class/Enum | Notes |
|---|---|---|
| Business/252 day count | `new Business252(new Brazil())` | Brazil calendar is the default calendar argument |
| Brazil Exchange calendar | `new Brazil(Brazil.Market.Exchange)` | B3 (formerly BM&FBovespa) trading calendar |
| Brazil Settlement calendar | `new Brazil(Brazil.Market.Settlement)` | CETIP/banking settlement days |
| IPCA inflation index | `new BRIBPCPA(...)` or `new ZeroInflationIndex(...)` | May need custom definition; use `ZeroInflationIndex` base class if named class is absent in the binding version |
| CPIBond (NTN-B proxy) | `new CPIBond(...)` | Inflation-indexed coupon bond; configure with the IPCA index and an observation lag |
| FixedRateBond (LTN/NTN-F) | `new FixedRateBond(...)` | Zero-coupon for LTN (single cash flow); annual coupon for NTN-F |
| OvernightIndex (CDI proxy) | `new OvernightIndex(...)` | CDI is an overnight index; no native `BRCdi` class — define it |
| OIS swap (CDI swap) | `new OvernightIndexedSwap(...)` | Pre × CDI structure |
| YieldTermStructure | `PiecewiseYieldCurve<Discount, LogLinear>` | Standard bootstrapped curve; use with Business252 and Brazil calendar |

**Gap:** QuantLib has no native `DI1` (B3 DI futures) instrument class. DI futures imply a rate via the `(100000 / PU)` formula and can be bootstrapped via a `DepositRateHelper` or a custom helper that accounts for B3 unit pricing. This will require a thin C# wrapper around the helper construction.

---

## What NOT to Use

| Package / Approach | Reason to Avoid |
|---|---|
| `NQuantLib`, `NQuantLib64`, `NQuantLib.dll` | All abandoned; wrap QuantLib versions from 2012–2016; .NET Framework only. |
| `QLNet` | Pure-C# re-implementation that diverges from upstream; lacks some instrument types and has different (non-QuantLib) API surface. Use only as a last resort. |
| `Excel-DNA` (old package name) | Legacy NuGet alias; superseded by `ExcelDna.AddIn`. |
| `ExcelDna.AddIn.NativeAOT` | Incompatible with SWIG/P/Invoke architecture in v1; defers all benefit until after the SWIG DLL deployment problem is solved. |
| `IsThreadSafe = true` on QuantLib UDFs | Causes concurrent access to shared QuantLib objects → crashes / corrupt results. |
| VSTO / COM add-in | Requires Visual Studio installer, admin rights, COM registration; ExcelDNA is strictly better for a pure-UDF library. |
| Sharing `Settings.EvaluationDate` across threads | QuantLib's evaluation date is a global per-thread; changing it in a background thread while another thread prices will silently produce wrong NPVs. |

---

## Alternatives Considered

| Category | Recommended | Alternative | Why Not |
|---|---|---|---|
| C# QuantLib bindings | `QuantLib` NuGet (official SWIG) | `QLNet` | QLNet diverges from upstream API; lacks some instruments; last NuGet release Nov 2024 (v1.13.1) still ~1 major version behind QuantLib C++. |
| C# QuantLib bindings | `QuantLib` NuGet (official SWIG) | Build from source (QuantLib-SWIG) | Valid but requires Boost + CMake + SWIG 4.3 + Visual Studio C++ toolchain on every dev machine; the NuGet package eliminates this entirely. |
| Target framework | `net8.0-windows` | `net9.0-windows` | .NET 9 is STS (short-term support, EOL May 2026); .NET 8 is LTS (EOL Nov 2026); stay on LTS. |
| Target framework | `net8.0-windows` | `net472` | .NET Framework is end-of-innovation; no new language features; no cross-target NuGet compatibility issues with modern packages. |
| Async model | `AsyncTaskUtil.RunTask` | `ExcelAsyncUtil.Run` | Both work; Task-based scales better and supports async/await. For v1 sync-only UDFs, the distinction is moot. |
| Deployment | Packed `.xll` | XCOPY multi-file | Packed is a single file; simpler for team distribution. |
| Logging | `ExcelDna.Diagnostics.Serilog` | NLog | Both work; Serilog has better structured logging support and is the community standard in recent ExcelDNA examples. |

---

## Installation Commands

```bash
# Add all packages to the project
dotnet add package ExcelDna.AddIn --version 1.9.0
dotnet add package QuantLib --version 1.42.1
dotnet add package ExcelDna.IntelliSense --version 1.9.0
dotnet add package ExcelDna.Diagnostics.Serilog --version 1.5.0
dotnet add package Serilog --version 4.2.0
dotnet add package Serilog.Sinks.File --version 5.0.0
```

---

## Sources

- [NuGet: ExcelDna.AddIn 1.9.0](https://www.nuget.org/packages/ExcelDna.AddIn/)
- [NuGet: QuantLib 1.42.1](https://www.nuget.org/packages/QuantLib/)
- [NuGet: ExcelDna.IntelliSense 1.9.0](https://www.nuget.org/packages/ExcelDna.IntelliSense)
- [NuGet: ExcelDna.Diagnostics.Serilog 1.5.0](https://www.nuget.org/packages/ExcelDna.Diagnostics.Serilog)
- [NuGet: NQuantLib64 1.0.9 (abandoned)](https://www.nuget.org/packages/NQuantLib64)
- [Excel-DNA Async Functions Guide](https://excel-dna.net/docs/guides-basic/asynchronous-functions/)
- [Excel-DNA Packing Tool](https://excel-dna.net/docs/archive/obsolete/exceldna-packing-tool/)
- [Excel-DNA Native AOT Support](https://excel-dna.net/docs/guides-basic/dotnet-native-aot-support/)
- [Excel-DNA SDK-Style Project Update](https://excel-dna.net/docs/guides-basic/updating-project-file-to-sdk-style/)
- [QuantLib-SWIG Repository](https://github.com/lballabio/QuantLib-SWIG)
- [QuantLib Ecosystem — Implementing QuantLib (2025)](https://www.implementingquantlib.com/2025/03/quantlib-ecosystem.html)
- [QuantLib Thread Safety — HPC-QuantLib](https://hpcquantlib.wordpress.com/2021/11/29/python-quantlib-and-multithreading/)
- [QuantLib Multi-Threading — HPC-QuantLib](https://hpcquantlib.wordpress.com/2013/07/26/multi-threading-and-quantlib/)
- [Segfault on multi-threaded C# app with QuantLib-SWIG #1795](https://github.com/lballabio/QuantLib/issues/1795)
- [BRL Interest Rate Swaps — Clarus FT](https://www.clarusft.com/brl-interest-rate-swaps/)
- [Business/252 Day Count — Clarus FT](https://www.clarusft.com/implementing-bus252-daycount-convention/)
- [QLNet GitHub (amaggiulli)](https://github.com/amaggiulli/QLNet)
- [QuantLib-Python Docs: Dates and Calendars](https://quantlib-python-docs.readthedocs.io/en/latest/dates.html)
- [ExcelDNA Object Handle Discussion (Google Group)](https://groups.google.com/g/exceldna/c/gphDZkh4HaE)
- [QLExtension: QuantLib + ExcelDNA Excel Add-in](https://github.com/dsimba/QLExtension)
- [DeepWiki: ExcelDNA Installation and Setup](https://deepwiki.com/Excel-DNA/ExcelDna/1.2-installation-and-setup)

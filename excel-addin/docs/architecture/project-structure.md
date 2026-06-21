# Project structure

The solution is split into four production assemblies with dependencies pointing inward toward stable contracts:

```text
Quant.Excel.AddIn --> Quant.QuantLib ------> Quant.Core
        |                                      ^
        +----------> Quant.Infrastructure -----+
        +--------------------------------------+
```

`Quant.Core` targets `net8.0` and owns stable public codes, interfaces, validation rules, and calculation orchestration. It is independent of Excel, infrastructure, and any QuantLib binding.

`Quant.QuantLib` targets `net8.0`, references `Quant.Core`, and will own the official QuantLib C++/SWIG integration, object catalogs, holiday corrections, and financial calculations. QLNet and a managed financial-calculation fallback are outside the architecture.

`Quant.Infrastructure` targets `net8.0`, references `Quant.Core`, and owns operational capabilities such as asynchronous diagnostics. It contains no financial algorithms and does not sit on successful calculation paths unless a contract explicitly requires it.

`Quant.Excel.AddIn` targets `net8.0-windows`, references all three other production projects, and is the thin Excel-DNA facade. It owns registration, Excel value conversion, argument normalization, and native Excel error translation. Builds produce only the 64-bit add-in. The `b` prefix is reserved for future Excel-DNA registration names; C# namespaces, types, and methods use ordinary `Quant.*` naming without that prefix.

Tests mirror the production boundaries: `Quant.Core.Tests` covers contracts, `Quant.QuantLib.Tests` covers native integration and adapters, and `Quant.Excel.AddIn.Tests` covers the Excel boundary plus cross-assembly architecture. The add-in tests target `net8.0-windows` because they reference the Windows-specific add-in; the other test projects target `net8.0`. Production C# lives only under `src/`; tests and tooling must not become runtime dependencies.

The successful calculation path must be typed and allocation-conscious. It performs no reflection, string parsing, file I/O, or logging. Native objects are initialized once and exposed through immutable catalogs so concurrent worksheet calls are read-only.

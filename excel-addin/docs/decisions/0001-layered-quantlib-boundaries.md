# 0001: Layered QuantLib boundaries

- Status: Accepted
- Date: 2026-06-20

## Context

The add-in must expose stable Excel functions while using official QuantLib C++ through its SWIG-generated .NET binding. It is 64-bit Windows-only and must keep successful worksheet calculations free of reflection, string parsing, file I/O, and logging. Direct wrappers would couple Excel types, native lifetimes, financial calculations, and operational services, making each change affect every layer. Reimplementing the financial path in managed code would create a second calculation authority and invite differences from QuantLib.

## Decision

Use a thin `Quant.Excel.AddIn` facade over stable contracts in `Quant.Core`, official native adapters in `Quant.QuantLib`, and operational services in `Quant.Infrastructure`.

Excel values and errors are translated by adapters at the add-in boundary; neither Excel-DNA types nor QuantLib SWIG types cross the core contracts. Static catalogs hold supported codes and reusable native objects. Explicit switch factories map stable public integer codes to typed implementations, rather than using enum ordinals, reflection, string parsing, or a general-purpose service container. Runtime catalogs are built during controlled initialization and are immutable afterward, so successful concurrent calculations are read-only and do not repeatedly construct native objects.

Only future Excel-DNA registration names receive the `b` prefix. Production C# remains under `src/` with `Quant.*` assembly, namespace, and type names.

## Consequences

The architecture adds interfaces, adapters, and composition code compared with direct wrappers. In return, Excel conversion, native integration, financial contracts, and infrastructure can evolve and be tested independently. QuantLib remains the sole calculation authority; there is no QLNet dependency and no managed calculation fast path. Explicit catalogs and switches require deliberate edits when supported codes change, but make the public mapping auditable and keep the hot path predictable. Immutable-after-initialization objects consume session-lifetime memory and require controlled shutdown, but avoid repeated construction and mutation races during worksheet calculation.

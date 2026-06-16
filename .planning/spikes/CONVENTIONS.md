# Spike Conventions — ExcelDNA + QuantLib SWIG Stack

Patterns and decisions established during spike validation.

## Stack & Targets

### .NET Runtime
- **Target Framework:** `net6.0-windows` (minimum)
- **Preferred:** `net8.0-windows` for LTS status and improved performance
- **Rationale:** Windows-only due to Excel/COM requirements. .NET Core for modern tooling.
- **NOT:** .NET Framework (legacy, no longer recommended for new projects)

### ExcelDNA
- **Package:** `ExcelDna.Integration` version 1.5.1+
- **Registration:** Use `ExcelDnaRegistration` API for explicit registration (not auto-loader)
- **Attribute Pattern:** `[ExcelFunction(Name = "PREFIX_FunctionName", Category = "Category")]`
- **Rationale:** Explicit registration more reliable on .NET Core; auto-loader has framework assumptions

### QuantLib
- **Binding Method:** SWIG-generated C# bindings
- **Source:** NuGet if available (`QuantLib.SWIG` preferred), else build from source
- **Native:** `QuantLib.dll` must be x64 (if app is x64); matching architecture essential
- **Deployment:** Bundled with add-in or via system PATH
- **Rationale:** SWIG bindings are production-ready; P/Invoke fully supported in .NET Core

## Architectural Patterns

### Handle Store (Caching Pattern)
```csharp
// Thread-safe cache for QuantLib objects
private static readonly ConcurrentDictionary<string, HandleEntry> Handles = ...;

// GUID-based opaque IDs returned to Excel
public static string CreateHandle(object quantLibObject) {
    var id = Guid.NewGuid().ToString("N");
    Handles[id] = new HandleEntry { Object = quantLibObject, RefCount = 1 };
    return id;
}

// Atomic reference counting via Interlocked
public static void ReleaseHandle(string id) {
    var newCount = Interlocked.Decrement(ref entry.RefCount);
    if (newCount <= 0) Handles.TryRemove(id, out _);
}
```
- **Use ConcurrentDictionary:** Provides thread-safe basic operations
- **Reference Counting:** Atomic via `Interlocked.Increment/Decrement`
- **Opaque IDs:** Users get GUIDs, not indices (prevents accidental reuse)
- **Rationale:** Proven safe for concurrent Excel UDF calls

### UDF Error Handling
```csharp
[ExcelFunction(Name = "QL_Something")]
public static object DoSomething(string curveHandle, double param) {
    try {
        var curve = HandleStore.Get(curveHandle);
        return curve.Compute(param);
    } catch (KeyNotFoundException) {
        return ExcelError.ExcelErrorValue;  // #VALUE!
    } catch (Exception ex) {
        Logging.Error($"UDF error: {ex}");
        return $"ERROR: {ex.Message}";  // User sees message in cell
    }
}
```
- **Return `ExcelError` for missing handles:** Cleaner than exceptions
- **Catch all exceptions:** UDF must not crash Excel
- **Log to file:** Excel won't show stack traces
- **Message propagation:** Return error string for user feedback

### Project Structure
```
src/
├── ExcelQuantLib/           # Main add-in project
│   ├── QuantLib.Integration/
│   │   ├── HandleStore.cs   # Core handle caching
│   │   ├── Facade.cs        # QuantLib wrapper API
│   │   └── Logging.cs       # File-based logging
│   │
│   └── UDFs/
│       ├── CurveUDFs.cs     # Yield curve functions
│       ├── DateUDFs.cs      # Date manipulation
│       └── RiskUDFs.cs      # Risk calculations
│
├── ExcelQuantLib.Tests/     # Unit + integration tests
│   ├── HandleStoreConcurrencyTests.cs
│   ├── UDFRegistrationTests.cs
│   └── QuantLibIntegrationTests.cs
│
└── ExcelQuantLib.Setup/     # Add-in installation/registration
    └── Installer.cs         # COM registration, PATH setup
```

## Development Practices

### Build & Deployment
- **Build configuration:** Release (debugging via logging, not debugger)
- **Output:** Single DLL per project + native QuantLib.dll dependency
- **Versioning:** Semantic versioning (1.0.0, 1.1.0, etc.)
- **NuGet:** Publish ExcelQuantLib to private NuGet for internal use

### Testing
- **Unit tests:** Handle store operations, error cases
- **Integration tests:** UDF registration, QuantLib calls through Excel
- **Stress tests:** 100+ concurrent cells, rapid create/destroy cycles
- **Error injection:** Simulate QuantLib failures (handle not found, invalid date, etc.)

### Logging
- **Pattern:** File-based, time-stamped entries to `%APPDATA%\ExcelQuantLib\logs\`
- **Levels:** DEBUG, INFO, WARN, ERROR
- **Content:** Function name, parameters (sanitized), result/error, elapsed time
- **Rotation:** Keep last 10 files, max 10MB each
- **Access:** Provide Excel UDF to retrieve/clear log for diagnostics

### Documentation
- **README:** How to install, first steps, troubleshooting
- **API.md:** Exported UDF names, parameters, return types, examples
- **ARCHITECTURE.md:** Design decisions, handle store rationale, concurrency model
- **TROUBLESHOOTING.md:** Common errors (DLL not found, invalid handle, etc.) + fixes

## Performance Targets

- **UDF call latency:** < 100ms typical (depends on QuantLib operation)
- **Handle store operations:** < 1ms (even with 10k handles)
- **Concurrent calls:** 8+ cores parallelized without contention
- **Memory:** < 500MB per 10k cached objects (depends on object size)

## Known Gotchas

1. **DLL Architecture Mismatch:** If app is x64, QuantLib.dll must be x64. Crashes otherwise.
2. **COM Registration:** May require admin privileges on some Windows versions. Plan for this.
3. **Debugging:** Can't attach debugger to Excel UDF easily. Use file logging instead.
4. **Version Skew:** Keep ExcelDna.Integration and QuantLib versions stable. Document them.
5. **Excel Threading:** UDFs may run on different threads; handle store must be thread-safe (✓ validated).

## Migration Path (If Needed)

If .NET Core + ExcelDNA + SWIG approach hits blocker:
- **Fallback 1:** Use .NET Framework 4.8 (same codebase, but legacy OS support only)
- **Fallback 2:** Use C++/CLI wrapper (direct C++ ↔ Excel, more complex)
- **Fallback 3:** Use alternative library (e.g., QuantLibXL if available)

None of these are preferred. Proceed with .NET Core approach; fallbacks only if validated blockers emerge.


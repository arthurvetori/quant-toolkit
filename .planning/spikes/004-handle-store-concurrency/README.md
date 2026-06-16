---
spike: 004
name: handle-store-concurrency
type: standard
validates: "Given a static handle store shared across concurrent UDF calls, can it maintain data integrity and prevent race conditions?"
verdict: PENDING
related: [001, 002, 003]
tags: [handles, concurrency, stress-test, critical]
---

# Spike 004: Handle Store Concurrency and Data Integrity

## What This Validates
**Given** multiple cells in Excel all calling UDFs that reference the same cached QuantLib object via a handle
**When** Excel recalculates (potentially parallel across cores)
**Then** each UDF receives the correct object and data remains consistent

**Success criteria:**
- No data corruption when multiple cells access same handle simultaneously
- Each UDF call gets correct handle on retrieval
- No deadlocks or hangs
- Object lifecycle is correct (created once, survives concurrent references, cleaned up only when all references gone)

## Background: Handle Pattern

QuantLib uses a "Handle" pattern (smart pointer) for managing shared objects:

```cpp
// C++ QuantLib pattern:
Handle<YieldTermStructure> curve = ...;  // Reference-counted smart pointer
// Multiple threads can safely hold the same Handle
// Underlying object persists until last Handle is destroyed
```

In Excel UDFs, we need to replicate this for .NET Core:

```csharp
// .NET Core pattern (what we're testing):
static Dictionary<string, object> HandleStore = new();

public static string CreateCurve(double rate) {
    var curveId = Guid.NewGuid().ToString();
    var curve = new FlatForwardCurve(rate);  // QuantLib object
    HandleStore[curveId] = curve;
    return curveId;  // Return opaque handle ID to user
}

public static double GetRate(string curveId, double time) {
    var curve = HandleStore[curveId];  // Must be thread-safe
    return curve.discount(time);
}
```

**Risk:** If Excel parallelizes UDF recalculation, concurrent dictionary access without locking causes:
- Corrupted dictionary state
- Lost or duplicated objects
- Incorrect return values
- Crashes

## How to Run

### Setup

```bash
cd .planning/spikes/004-handle-store-concurrency
dotnet new nunit -n HandleStoreTests -f net6.0
cd HandleStoreTests
# Add reference to implementation
dotnet add project ../003-udf-registration-and-call/...csproj
dotnet add package NUnit
```

### Execution

```bash
# Run test suite
dotnet test

# Output should show:
# - HandleStore_ConcurrentCreation: PASS
# - HandleStore_ConcurrentRetrieval: PASS
# - HandleStore_RaceCondition_DuplicateCreation: PASS
# - HandleStore_Cleanup: PASS
```

### Stress Test (Optional, Advanced)

```bash
# Run with high concurrency (32 threads, 10,000 iterations)
dotnet test -- --stress-test --threads 32 --iterations 10000

# Monitor:
# - Total handles created
# - Total handles destroyed
# - Any corruption detected
```

## What to Expect

### Happy Path
- All concurrent operations succeed
- Dictionary state remains consistent
- No exceptions thrown

### Edge Cases
- Concurrent creation of duplicate keys → Handled gracefully (one wins, others fail)
- Concurrent retrieval of expired/garbage-collected handles → Clear error message
- Rapid creation and deletion → No leaks or crashes

### Performance
- < 1ms per operation (typical)
- No significant lock contention
- Scales to Excel's typical parallelization level (4-8 cores)

## Investigation Trail

### Phase 1: Basic Thread Safety (Starting)
- [ ] Implement HandleStore with ConcurrentDictionary
- [ ] Write basic concurrent creation test
- [ ] Verify no exceptions

### Phase 2: Advanced Scenarios (Pending)
- [ ] Test rapid creation/deletion cycles
- [ ] Test interleaved reads and writes
- [ ] Test with real QuantLib objects (not mock)

### Phase 3: Stress Test (Pending)
- [ ] Run with 1000+ concurrent operations
- [ ] Verify no data corruption
- [ ] Check performance profile

### Phase 4: Excel Simulation (Pending)
- [ ] Create workbook with 100+ cells referencing same handle
- [ ] Force recalculation (Ctrl+Shift+F9)
- [ ] Verify results are consistent across all cells

### Phase 5: Assessment (Pending)
- [ ] Confirm handle store is production-ready
- [ ] Document any limitations or gotchas
- [ ] Verify safe for Phase 1 implementation

## Results
*To be updated after concurrency testing*


---
spike: 003
name: udf-registration-and-call
type: standard
validates: "Given a .NET Core DLL with ExcelDNA attributes and QuantLib references, when loaded into Excel, can UDFs be registered and successfully executed from worksheet cells?"
verdict: PENDING
related: [001, 002, 004]
tags: [udf, excel, exceldna, integration-test]
---

# Spike 003: UDF Registration and Execution

## What This Validates
**Given** a .NET Core DLL with [ExcelFunction] attributes referencing QuantLib SWIG bindings
**When** Excel loads the DLL and a worksheet calls the UDF
**Then** the function executes successfully and returns expected results

**Success criteria:**
- Excel recognizes the DLL as an add-in
- UDFs appear in Excel's function list
- Calling a UDF from a worksheet cell produces correct output
- QuantLib calls within UDFs execute without errors

## How to Run

### Setup Phase

```bash
# 1. Build combined project (outputs DLL with ExcelDNA + QuantLib references)
cd .planning/spikes/003-udf-registration-and-call
dotnet build

# 2. Register DLL with Excel (manual for now)
# - Open Excel
# - Go to File → Options → Trust Center → Trust Center Settings → Trusted Locations
# - Add path to .planning/spikes/003-udf-registration-and-call/bin/Release/net6.0/
# - OR use ExcelDNA loader if available

# 3. Load the add-in via Tools → Add-ins (if registered)
```

### Execution Phase

```
In Excel worksheet:

A1: =HelloCore()
   Expected: "Hello from .NET Core!"

B1: =Add(5, 3)
   Expected: 8

C1: =QuantLibDate()
   Expected: [Current date in QuantLib format]
```

## What to Expect

### Registration
- Excel loads the DLL without crashing
- Functions appear in function wizard (Ctrl+Shift+F9 or Insert → Function)
- Function categories match [ExcelFunction(Category = "...")] attributes

### Execution (Happy Path)
- Formulas calculate without error
- Results match expected values
- No "Ref!" or "#NAME?" errors

### Execution (Edge Cases)
- Calling multiple UDFs in same formula
- Multiple cells calling same UDF
- Mixing ExcelDNA-only UDFs with QuantLib-based UDFs

### Observability
- Error messages are captured and reported to Excel (not just logged)
- If QuantLib call fails, user sees meaningful error in cell

## Investigation Trail

### Phase 1: Project Setup (Starting)
- [ ] Create class library with both ExcelDNA and QuantLib references
- [ ] Define UDFs that use both libraries
- [ ] Build project successfully

### Phase 2: Registration Mechanics (Pending)
- [ ] Document registration process for Excel + .NET Core
- [ ] Test loading via ExcelDNA loader or manual registry
- [ ] Verify functions appear in function wizard

### Phase 3: Basic UDF Execution (Pending)
- [ ] Call simple UDF from worksheet
- [ ] Verify result is correct
- [ ] Check for crashes or exceptions

### Phase 4: QuantLib Integration (Pending)
- [ ] Create UDF that calls QuantLib (e.g., date arithmetic)
- [ ] Execute from Excel
- [ ] Verify QuantLib functionality works through UDF boundary

### Phase 5: Assessment (Pending)
- [ ] Confirm all success criteria met
- [ ] Document any workarounds needed
- [ ] Identify gotchas for Phase 1 implementation

## Results
*To be updated after integration testing*


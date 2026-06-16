---
spike: 001
name: exceldna-dotnet-core-setup
type: standard
validates: "Given ExcelDNA + .NET Core (not Framework), when building and loading a DLL, can Excel successfully recognize it as an add-in?"
verdict: PENDING
related: [002, 003, 004]
tags: [exceldna, dotnet-core, setup, foundation]
---

# Spike 001: ExcelDNA .NET Core Setup Validation

## What This Validates
**Given** ExcelDNA source and .NET Core SDK tooling
**When** building a minimal DLL with ExcelDNA attributes
**Then** Excel can load the DLL as an add-in and recognize it as a valid extension

**Success criteria:**
- Visual Studio or dotnet CLI can create and build a .NET Core class library
- ExcelDNA integration builds successfully
- The resulting DLL is loadable by Excel (via ExcelDNA .xll stub or direct registration)

## Research

### ExcelDNA Status with .NET Core
ExcelDNA (https://github.com/Excel-DNA/ExcelDna) has been moving toward .NET Core support but has historically been tied to .NET Framework due to Office COM interop constraints. Key findings:

| Aspect | Status | Notes |
|--------|--------|-------|
| ExcelDNA 2.1+ | Partial Support | Can target .NET Core in newer versions |
| COM Interop | Framework required | Office COM requires .NET Framework on Windows |
| ExcelDnaRegistration | Middleware | Explicit registration needed for .NET Core |
| XLL Loading | Framework required | Native .xll stub requires Framework target |

**Chosen approach:** Use ExcelDNA 2.1+ with explicit COM registration pattern targeting .NET Core, acknowledging that the runtime may still require Framework shim layer.

## How to Run

```bash
# 1. Create test project
cd .planning/spikes/001-exceldna-dotnet-core-setup
dotnet new classlib -n TestExcelDNA -f net6.0

# 2. Add ExcelDNA nuget
cd TestExcelDNA
dotnet add package ExcelDna.Integration

# 3. Create hello-world UDF
# (See prototype-basic-udf.cs in this directory)

# 4. Build the project
dotnet build

# 5. Check the output DLL
ls -la bin/Release/net6.0/TestExcelDNA.dll
```

## What to Expect
- Successful `dotnet build` with no errors or warnings
- A DLL file in `bin/Release/net6.0/` that is loadable
- Ability to inspect DLL with ildasm or dnSpy to confirm ExcelFunction attributes are embedded

## Investigation Trail

### Phase 1: Project Creation & Build (Starting)
- [ ] Create .NET Core 6.0 class library
- [ ] Add ExcelDNA.Integration NuGet package
- [ ] Attempt to build without any UDFs
- [ ] Document any compiler/runtime warnings

### Phase 2: UDF Definition (Pending)
- [ ] Define a simple [ExcelFunction] method
- [ ] Build and verify compilation
- [ ] Inspect DLL metadata for ExcelDNA attributes

### Phase 3: COM Interop Probe (Pending)
- [ ] Document which ExcelDNA features require COM
- [ ] Test loading DLL in Excel (manual or via ExcelDNA loader)
- [ ] Identify any runtime registration blockers

### Phase 4: Assessment (Pending)
- [ ] Summarize .NET Core feasibility
- [ ] Document any required compromises or workarounds
- [ ] Recommend proceed or pivot

## Results
*To be updated after running builds*


# Quant Foundation and Native Build Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a clean x64 .NET 8/Excel-DNA solution that reproducibly builds and loads the official QuantLib 1.42.1 C# SWIG bindings.

**Architecture:** Production projects live under `src/`, tests mirror project boundaries, and official native sources are pinned as git submodules under `external/`. `Quant.QuantLib` is the only project allowed to reference `NQuantLib`; the Excel project consumes it through project interfaces.

**Tech Stack:** C# 12, .NET 8, ExcelDna.AddIn 1.9.0, QuantLib 1.42.1, QuantLib-SWIG 1.42.1, Visual Studio 2022 C++ Build Tools, xUnit 2.9.3.

## Global Constraints

- Support 64-bit Excel on Windows only.
- Use official QuantLib C++ and official QuantLib-SWIG C# bindings; do not use QLNet.
- Target `net8.0` for libraries/tests and `net8.0-windows` for the Excel add-in.
- Put all production C# code below `src/`; third-party source belongs below `external/`.
- Use `Quant.*` project and namespace names.
- Use the `b` prefix only in Excel-DNA `Name` registrations, never in C# method names.
- Keep the successful calculation path free of reflection, string enum parsing, file I/O, and logging.
- Update build documentation in the same task as build behavior.
- Preserve unrelated staged or working-tree changes; path-limit every commit.

---

### Task 1: Scaffold the solution and enforce project boundaries

**Files:**
- Create: `global.json`
- Create: `Directory.Build.props`
- Create: `Directory.Packages.props`
- Create: `.gitignore`
- Create: `Quant.sln`
- Create: `src/Quant.Core/Quant.Core.csproj`
- Create: `src/Quant.Core/AssemblyMarker.cs`
- Create: `src/Quant.QuantLib/Quant.QuantLib.csproj`
- Create: `src/Quant.QuantLib/AssemblyMarker.cs`
- Create: `src/Quant.Infrastructure/Quant.Infrastructure.csproj`
- Create: `src/Quant.Infrastructure/AssemblyMarker.cs`
- Create: `src/Quant.Excel.AddIn/Quant.Excel.AddIn.csproj`
- Create: `src/Quant.Excel.AddIn/AssemblyMarker.cs`
- Create: `tests/Quant.Core.Tests/Quant.Core.Tests.csproj`
- Create: `tests/Quant.Core.Tests/Architecture/ProjectBoundaryTests.cs`
- Create: `tests/Quant.QuantLib.Tests/Quant.QuantLib.Tests.csproj`
- Create: `tests/Quant.Excel.AddIn.Tests/Quant.Excel.AddIn.Tests.csproj`
- Create: `docs/architecture/project-structure.md`
- Create: `docs/decisions/0001-layered-quantlib-boundaries.md`

**Interfaces:**
- Consumes: none.
- Produces: four buildable production assemblies named `Quant.Core`, `Quant.QuantLib`, `Quant.Infrastructure`, and `Quant.Excel.AddIn`.

- [ ] **Step 1: Write the boundary test first**

```csharp
using Xunit;

namespace Quant.Core.Tests.Architecture;

public sealed class ProjectBoundaryTests
{
    [Fact]
    public void AssembliesUseQuantNames()
    {
        Assert.Equal("Quant.Core", typeof(Quant.Core.AssemblyMarker).Assembly.GetName().Name);
        Assert.Equal("Quant.QuantLib", typeof(Quant.QuantLib.AssemblyMarker).Assembly.GetName().Name);
        Assert.Equal("Quant.Infrastructure", typeof(Quant.Infrastructure.AssemblyMarker).Assembly.GetName().Name);
        Assert.Equal("Quant.Excel.AddIn", typeof(Quant.Excel.AddIn.AssemblyMarker).Assembly.GetName().Name);
    }
}
```

- [ ] **Step 2: Run the test and confirm the missing-project failure**

Run: `dotnet test tests/Quant.Core.Tests/Quant.Core.Tests.csproj -c Release`

Expected: FAIL because the projects and marker types do not exist.

- [ ] **Step 3: Create central build configuration and projects**

Use this central configuration:

```json
{
  "sdk": {
    "version": "8.0.100",
    "rollForward": "latestFeature"
  }
}
```

```xml
<!-- Directory.Build.props -->
<Project>
  <PropertyGroup>
    <LangVersion>12.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Deterministic>true</Deterministic>
    <PlatformTarget>x64</PlatformTarget>
  </PropertyGroup>
</Project>
```

```xml
<!-- Directory.Packages.props -->
<Project>
  <PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="ExcelDna.AddIn" Version="1.9.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.6.0" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
  </ItemGroup>
</Project>
```

Each library project targets `net8.0`. `Quant.Excel.AddIn.csproj` targets `net8.0-windows`, references the other three production projects, and contains:

```xml
<PropertyGroup>
  <TargetFramework>net8.0-windows</TargetFramework>
  <ExcelDnaCreate32BitAddIn>false</ExcelDnaCreate32BitAddIn>
  <ExcelDnaCreate64BitAddIn>true</ExcelDnaCreate64BitAddIn>
  <ExcelAddInExplicitExports>true</ExcelAddInExplicitExports>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="ExcelDna.AddIn" />
</ItemGroup>
```

Every marker file contains its project namespace and one empty sealed type, for example:

```csharp
namespace Quant.Core;
public sealed class AssemblyMarker { }
```

- [ ] **Step 4: Add projects to the solution and run all tests**

Run:

```powershell
dotnet new sln --name Quant --force
dotnet sln Quant.sln add src/Quant.Core/Quant.Core.csproj src/Quant.QuantLib/Quant.QuantLib.csproj src/Quant.Infrastructure/Quant.Infrastructure.csproj src/Quant.Excel.AddIn/Quant.Excel.AddIn.csproj tests/Quant.Core.Tests/Quant.Core.Tests.csproj tests/Quant.QuantLib.Tests/Quant.QuantLib.Tests.csproj tests/Quant.Excel.AddIn.Tests/Quant.Excel.AddIn.Tests.csproj
dotnet test Quant.sln -c Release
```

Expected: all tests PASS and all projects build for x64.

- [ ] **Step 5: Document the boundaries and their trade-offs**

`docs/architecture/project-structure.md` must describe dependency direction and the responsibility of every project. `docs/decisions/0001-layered-quantlib-boundaries.md` records why thin facade, adapter, static catalog, explicit switch factory, and immutable-after-initialization patterns were chosen over direct wrappers and a managed calculation fast path.

- [ ] **Step 6: Commit only the scaffold**

```powershell
git add -- global.json Directory.Build.props Directory.Packages.props .gitignore Quant.sln src tests docs/architecture/project-structure.md docs/decisions/0001-layered-quantlib-boundaries.md
git commit -m "build: scaffold Quant add-in solution"
```

### Task 2: Pin and build official QuantLib SWIG for x64

**Files:**
- Create: repository-root `../.gitmodules`
- Create: `external/QuantLib` (gitlink pinned to `v1.42.1`)
- Create: `external/QuantLib-SWIG` (gitlink pinned to `v1.42.1`)
- Create: `eng/build-native.ps1`
- Modify: `src/Quant.QuantLib/Quant.QuantLib.csproj`
- Create: `tests/Quant.QuantLib.Tests/Interop/OfficialBindingSmokeTests.cs`
- Create: `docs/native-build/windows-x64.md`

**Interfaces:**
- Consumes: the `Quant.QuantLib` project from Task 1.
- Produces: a project reference to official `NQuantLib`, plus `NQuantLibc.dll` copied beside every managed consumer.

- [ ] **Step 1: Add pinned official repositories**

Run from `excel-addin/`:

```powershell
git -C .. submodule add https://github.com/lballabio/QuantLib.git excel-addin/external/QuantLib
git -C external/QuantLib checkout v1.42.1
git -C .. submodule add https://github.com/lballabio/QuantLib-SWIG.git excel-addin/external/QuantLib-SWIG
git -C external/QuantLib-SWIG checkout v1.42.1
```

Expected: both gitlinks point to the commits tagged `v1.42.1`.

- [ ] **Step 2: Write the failing official-binding smoke test**

```csharp
using QL = QuantLib;
using Xunit;

namespace Quant.QuantLib.Tests.Interop;

public sealed class OfficialBindingSmokeTests
{
    [Fact]
    public void OfficialSwigBindingCreatesDate()
    {
        using var date = new QL.Date(20, QL.Month.June, 2026);
        Assert.Equal(20, date.dayOfMonth());
        Assert.Equal(QL.Month.June, date.month());
        Assert.Equal(2026, date.year());
    }
}
```

- [ ] **Step 3: Reference the official managed project and native output**

Add to `Quant.QuantLib.csproj`:

```xml
<ItemGroup>
  <ProjectReference Include="..\..\external\QuantLib-SWIG\CSharp\csharp\NQuantLib.csproj" />
  <None Include="..\..\external\QuantLib-SWIG\CSharp\cpp\NQuantLibc.dll"
        Link="NQuantLibc.dll"
        CopyToOutputDirectory="PreserveNewest"
        CopyToPublishDirectory="PreserveNewest" />
</ItemGroup>
```

Run: `dotnet test tests/Quant.QuantLib.Tests/Quant.QuantLib.Tests.csproj -c Release`

Expected: FAIL because `NQuantLibc.dll` has not been built.

- [ ] **Step 4: Implement the reproducible native build script**

`eng/build-native.ps1` must locate Visual Studio through `vswhere`, set `QL_DIR` to `external/QuantLib`, and run:

```powershell
& $msbuild "$repoRoot\external\QuantLib\QuantLib.sln" /m /p:Configuration=Release /p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw "QuantLib x64 build failed." }

$env:QL_DIR = "$repoRoot\external\QuantLib"
& $msbuild "$repoRoot\external\QuantLib-SWIG\CSharp\QuantLib.sln" /m /p:Configuration=Release /p:Platform=x64
if ($LASTEXITCODE -ne 0) { throw "QuantLib-SWIG x64 build failed." }

dotnet build "$repoRoot\external\QuantLib-SWIG\CSharp\csharp\NQuantLib.csproj" -c Release -f net8.0
if ($LASTEXITCODE -ne 0) { throw "NQuantLib managed build failed." }
```

The script must fail early when the C++ x64 workload, submodules, or expected `NQuantLibc.dll` output is missing.

- [ ] **Step 5: Build native dependencies and rerun the smoke test**

Run:

```powershell
.\eng\build-native.ps1
dotnet test tests/Quant.QuantLib.Tests/Quant.QuantLib.Tests.csproj -c Release
```

Expected: `OfficialSwigBindingCreatesDate` PASS and `NQuantLibc.dll` exists in the test output directory.

- [ ] **Step 6: Document prerequisites and exact commands**

`docs/native-build/windows-x64.md` must state Visual Studio 2022 C++ workload, .NET 8 SDK, SWIG/native version `1.42.1`, submodule initialization, build command, expected DLL locations, and common architecture-mismatch diagnosis.

- [ ] **Step 7: Commit the pinned native integration**

```powershell
git add -- ../.gitmodules external/QuantLib external/QuantLib-SWIG eng/build-native.ps1 src/Quant.QuantLib/Quant.QuantLib.csproj tests/Quant.QuantLib.Tests/Interop/OfficialBindingSmokeTests.cs docs/native-build/windows-x64.md
git commit -m "build: integrate official QuantLib SWIG x64"
```

### Task 3: Produce and verify an x64-only Excel-DNA artifact

**Files:**
- Create: `src/Quant.Excel.AddIn/AddInLifecycle.cs`
- Create: `eng/verify-package.ps1`
- Create: `docs/functions/loading-the-add-in.md`
- Modify: `README.md`

**Interfaces:**
- Consumes: the official native reference from Task 2.
- Produces: a packed x64 XLL containing the managed assemblies and `NQuantLibc.dll`.

- [ ] **Step 1: Write the packaging verification script and test invocation**

The script must build Release, require at least one `*64*.xll`, reject any 32-bit XLL, and require `NQuantLibc.dll` in the unpacked build output:

```powershell
dotnet build "$repoRoot\src\Quant.Excel.AddIn\Quant.Excel.AddIn.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw "Excel add-in build failed." }

$xll = Get-ChildItem "$repoRoot\src\Quant.Excel.AddIn\bin\Release" -Recurse -Filter '*64*.xll'
if ($xll.Count -eq 0) { throw "No x64 XLL was produced." }
if (Get-ChildItem "$repoRoot\src\Quant.Excel.AddIn\bin\Release" -Recurse -Filter '*.xll' | Where-Object Name -NotMatch '64') {
    throw "A non-x64 XLL was produced."
}
if (-not (Get-ChildItem "$repoRoot\src\Quant.Excel.AddIn\bin\Release" -Recurse -Filter 'NQuantLibc.dll')) {
    throw "NQuantLibc.dll was not copied."
}
```

- [ ] **Step 2: Add the Excel-DNA lifecycle class**

```csharp
using ExcelDna.Integration;

namespace Quant.Excel.AddIn;

public sealed class AddInLifecycle : IExcelAddIn
{
    public void AutoOpen() { }
    public void AutoClose() { }
}
```

Do not initialize calculations or logging yet; those are owned by later plans.

- [ ] **Step 3: Run package verification**

Run: `.\eng\verify-package.ps1`

Expected: PASS with only x64 XLL output and the native wrapper present.

- [ ] **Step 4: Document loading and manual smoke verification**

Document Excel `File > Options > Add-ins`, selecting the x64 XLL, expected successful load, and diagnosis for missing .NET 8 or VC++ runtime. State that worksheet functions arrive in the calendar/day-count plan.

- [ ] **Step 5: Commit packaging and documentation**

```powershell
git add -- src/Quant.Excel.AddIn/AddInLifecycle.cs eng/verify-package.ps1 docs/functions/loading-the-add-in.md README.md
git commit -m "build: package x64 Excel-DNA add-in"
```

## Plan Verification

Run:

```powershell
.\eng\build-native.ps1
dotnet test Quant.sln -c Release
.\eng\verify-package.ps1
```

Expected: all tests pass, the official QuantLib binding smoke test executes, and only an x64 XLL is produced.

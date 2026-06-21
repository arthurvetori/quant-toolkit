# Official QuantLib native build on Windows x64

This repository uses only the official QuantLib and QuantLib-SWIG bindings. Both submodules are pinned to the `v1.42.1` release:

- QuantLib commit `099987f0ca2c11c505dc4348cdb9ce01a598e1e5`
- QuantLib-SWIG commit `34e9247f3b6008725517cd5359d9fbe64e52aa21`

QLNet is not used. The supported native architecture is x64 only.

## Prerequisites

- 64-bit Windows.
- Visual Studio 2022 or Visual Studio 2022 Build Tools with the **Desktop development with C++** workload. Include the MSVC v143 x64/x86 build tools and a Windows SDK. The build script locates this installation with `vswhere.exe` and rejects installations without the x64 compiler component.
- .NET 8 SDK. `global.json` selects .NET 8, and the managed `Quant.QuantLib` and test projects target `net8.0`.
- Git, including submodule support.
- Boost source headers. Set `BOOST_ROOT` to the extracted source directory containing `boost\config.hpp`. Boost 1.86.0 is the verified version.
- SWIG for Windows. Set `SWIG_EXE` to `swig.exe`. SWIG 4.2.1 is the verified version.

The official QuantLib-SWIG `v1.42.1` submodule contains the interface definitions, but not generated C++ and C# bindings. The build script runs the pinned SWIG executable before compiling the wrapper.

## Initialize the pinned sources

Run from the repository root, the directory that contains `.gitmodules`:

```powershell
git submodule update --init --recursive excel-addin/external/QuantLib excel-addin/external/QuantLib-SWIG
git -C excel-addin/external/QuantLib describe --tags --exact-match
git -C excel-addin/external/QuantLib-SWIG describe --tags --exact-match
```

Both tag checks must print `v1.42.1`. The build script also verifies the exact pinned commits before invoking native tools.

## Build and test

Run from `excel-addin`:

```powershell
$env:BOOST_ROOT = 'C:\path\to\boost_1_86_0'
$env:SWIG_EXE = 'C:\path\to\swigwin-4.2.1\swig.exe'
.\eng\build-native.ps1
dotnet test .\tests\Quant.QuantLib.Tests\Quant.QuantLib.Tests.csproj -c Release --nologo
```

The script performs these builds in order:

1. `external\QuantLib\QuantLib.sln`, `Release|x64`, with Visual Studio 2022 MSBuild.
2. QuantLib-SWIG C++ and C# source generation from `SWIG\quantlib.i`.
3. `external\QuantLib-SWIG\CSharp\cpp\QuantLibWrapper.vcxproj`, `Release|x64`, with `QL_DIR` set to the pinned QuantLib submodule.
4. `external\QuantLib-SWIG\CSharp\csharp\NQuantLib.csproj`, `Release|net8.0`, with the .NET SDK.

The repository-level `Directory.Build.targets` constrains the official multi-target `NQuantLib` project to `net8.0` and suppresses the known `CS0108` warning in generated upstream code. All projects continue to treat other warnings as errors. The official generated source remains unchanged.

## Expected outputs

- QuantLib static import library: `external\QuantLib\lib\QuantLib-x64-mt.lib`
- Native SWIG build output: `external\QuantLib-SWIG\CSharp\cpp\bin\x64\Release\NQuantLibc.dll`
- Native DLL copied by the official post-build step: `external\QuantLib-SWIG\CSharp\cpp\NQuantLibc.dll`
- Managed binding: `external\QuantLib-SWIG\CSharp\csharp\bin\Release\net8.0\NQuantLib.dll`
- Managed-test native copy: `tests\Quant.QuantLib.Tests\bin\Release\net8.0\NQuantLibc.dll`

`Quant.QuantLib.csproj` copies `NQuantLibc.dll` beside every managed consumer during build and publish. The smoke test creates an official `QuantLib.Date`, proving that both the managed binding and its native x64 DLL load successfully.

## Diagnose architecture mismatches

`BadImageFormatException`, Windows error 193, or a load failure despite the DLL being present usually means that an x86 binary or one of its dependencies was selected by an x64 process.

Open a **Developer PowerShell for VS 2022** and inspect the native image:

```powershell
dumpbin /headers .\external\QuantLib-SWIG\CSharp\cpp\NQuantLibc.dll | Select-String 'machine'
dumpbin /dependents .\external\QuantLib-SWIG\CSharp\cpp\NQuantLibc.dll
[Environment]::Is64BitProcess
```

The headers must report `machine (x64)`, and the process check must return `True`. Remove stale Win32 outputs, especially any old `CSharp\cpp\NQuantLibc.dll`, and rerun `eng\build-native.ps1`. Do not copy a Win32 DLL into an x64 test or Excel-DNA output directory. If the machine type is x64 but loading still fails, use `dumpbin /dependents` to find a missing MSVC runtime or other native dependency and confirm that the Visual C++ 2015-2022 x64 runtime is installed.

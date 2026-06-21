# Quant Excel add-in

An x64-only .NET 8 Excel-DNA add-in backed by the official QuantLib 1.42.1 C++ library and QuantLib-SWIG C# bindings.

## Build

Follow [the Windows x64 native build guide](docs/native-build/windows-x64.md) to configure Visual Studio Build Tools, Boost, and SWIG. Then run:

```powershell
.\eng\build-native.ps1
dotnet test .\Quant.sln -c Release
.\eng\verify-package.ps1
```

The distributable artifact is:

`src\Quant.Excel.AddIn\bin\Release\net8.0-windows\publish\Quant.Excel.AddIn-AddIn64-packed.xll`

See [loading the add-in](docs/functions/loading-the-add-in.md) for Excel installation and troubleshooting steps. Calendar and day-count worksheet functions are implemented in the next development phase.

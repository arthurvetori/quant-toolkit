# Load the x64 Excel add-in

The supported artifact is the packed 64-bit XLL:

`src\Quant.Excel.AddIn\bin\Release\net8.0-windows\publish\Quant.Excel.AddIn-AddIn64-packed.xll`

It contains the managed add-in assemblies, the official `NQuantLib` binding, and the x64 `NQuantLibc.dll` native wrapper. Do not load the unpacked XLL by itself and do not use this add-in with 32-bit Excel.

## Build and verify

Build the native dependencies first, following [the native build guide](../native-build/windows-x64.md), then run:

```powershell
.\eng\verify-package.ps1
```

The verifier rejects 32-bit XLL output and inspects the packed XLL for both managed and native QuantLib resources.

## Load in Excel

1. Open 64-bit Microsoft Excel.
2. Select **File > Options > Add-ins**.
3. At the bottom, choose **Excel Add-ins** in the **Manage** list and select **Go**.
4. Select **Browse**, choose `Quant.Excel.AddIn-AddIn64-packed.xll`, and select **OK**.
5. Confirm the add-in remains checked in the **Add-ins available** list.

A successful load produces no error dialog. Worksheet functions are introduced by the calendar and day-count implementation plan; this foundation artifact only verifies loading and lifecycle integration.

## Troubleshooting

- If Excel reports that the file format or extension is invalid, confirm Excel is 64-bit and that the filename contains `AddIn64`.
- If the .NET runtime cannot be loaded, install the .NET 8 Desktop Runtime for x64 and restart Excel.
- If `NQuantLibc.dll` or one of its dependencies cannot be loaded, install the Microsoft Visual C++ 2015-2022 Redistributable for x64. Re-run `eng\verify-package.ps1` to confirm the native DLL is embedded.
- If Windows blocks a downloaded XLL, open the file's **Properties**, select **Unblock** when present, and try again.

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot 'Quant.sln'
$project = Join-Path $repoRoot 'src\Quant.Excel.AddIn\Quant.Excel.AddIn.csproj'
$releaseRoot = Join-Path $repoRoot 'src\Quant.Excel.AddIn\bin\Release'

& dotnet test $solution -c Release --nologo '-m:1' '/p:UseSharedCompilation=false'
if ($LASTEXITCODE -ne 0) {
    throw 'Release test suite or approved Excel registration inspection failed.'
}

& dotnet build $project -c Release --nologo '-m:1' '/p:UseSharedCompilation=false'
if ($LASTEXITCODE -ne 0) {
    throw 'Excel add-in build failed.'
}

$x64Xlls = @(Get-ChildItem $releaseRoot -Recurse -Filter '*64*.xll')
if ($x64Xlls.Count -eq 0) {
    throw 'No x64 XLL was produced.'
}

$nonX64Xlls = @(Get-ChildItem $releaseRoot -Recurse -Filter '*.xll' | Where-Object Name -NotMatch '64')
if ($nonX64Xlls.Count -ne 0) {
    throw ('A non-x64 XLL was produced: ' + ($nonX64Xlls.FullName -join ', '))
}

$nativeDlls = @(Get-ChildItem $releaseRoot -Recurse -Filter 'NQuantLibc.dll')
if ($nativeDlls.Count -eq 0) {
    throw 'NQuantLibc.dll was not copied.'
}

$packedXlls = @($x64Xlls | Where-Object Name -Match 'packed')
if ($packedXlls.Count -eq 0) {
    throw 'No packed x64 XLL was produced.'
}

foreach ($packedXll in $packedXlls) {
    $resourceText = [Text.Encoding]::Unicode.GetString([IO.File]::ReadAllBytes($packedXll.FullName))
    if (-not $resourceText.Contains('NQUANTLIB')) {
        throw ('NQuantLib.dll was not packed into ' + $packedXll.FullName + '.')
    }
    if (-not $resourceText.Contains('NQUANTLIBC.DLL')) {
        throw ('NQuantLibc.dll was not packed into ' + $packedXll.FullName + '.')
    }
}

Write-Host ('Verified x64 XLL: ' + ($x64Xlls.FullName -join ', '))
Write-Host ('Verified native wrapper: ' + ($nativeDlls.FullName -join ', '))
Write-Host ('Verified packed QuantLib resources: ' + ($packedXlls.FullName -join ', '))
Write-Host 'Verified full Release test suite and exactly 17 approved Excel registrations.'

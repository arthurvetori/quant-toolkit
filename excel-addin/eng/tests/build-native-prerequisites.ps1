[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$buildScript = Join-Path $repoRoot 'eng\build-native.ps1'
$invalidBoostRoot = Join-Path $env:TEMP 'quant-missing-boost'
$previousBoostRoot = $env:BOOST_ROOT

try {
    $env:BOOST_ROOT = $invalidBoostRoot
    $output = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $buildScript 2>&1 | Out-String
    $exitCode = $LASTEXITCODE
}
finally {
    $env:BOOST_ROOT = $previousBoostRoot
}

if ($exitCode -eq 0) {
    throw 'build-native.ps1 unexpectedly accepted a missing Boost root.'
}

$expected = 'BOOST_ROOT must point to a Boost source tree containing boost\config.hpp'
if (-not $output.Contains($expected)) {
    throw ('Expected the Boost prerequisite error, but received:' + [Environment]::NewLine + $output)
}

Write-Host 'Native build rejects a missing Boost source tree.'

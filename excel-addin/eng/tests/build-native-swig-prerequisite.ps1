[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Continue'

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$buildScript = Join-Path $repoRoot 'eng\build-native.ps1'
$previousBoostRoot = $env:BOOST_ROOT
$previousSwigExe = $env:SWIG_EXE
$fakeBoostRoot = Join-Path $env:TEMP ('quant-fake-boost-' + $PID)
$fakeBoostDirectory = Join-Path $fakeBoostRoot 'boost'
[void][IO.Directory]::CreateDirectory($fakeBoostDirectory)
[IO.File]::WriteAllText((Join-Path $fakeBoostDirectory 'config.hpp'), [string]::Empty)

try {
    $env:BOOST_ROOT = $fakeBoostRoot
    $env:SWIG_EXE = Join-Path $env:TEMP 'quant-missing-swig.exe'
    $output = & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $buildScript 2>&1 | Out-String
    $exitCode = $LASTEXITCODE
}
finally {
    $env:BOOST_ROOT = $previousBoostRoot
    $env:SWIG_EXE = $previousSwigExe
    if ([IO.Directory]::Exists($fakeBoostRoot)) {
        [IO.Directory]::Delete($fakeBoostRoot, $true)
    }
}

if ($exitCode -eq 0) {
    throw 'build-native.ps1 unexpectedly accepted a missing SWIG executable.'
}

$expected = 'SWIG_EXE must point to swig.exe'
if (-not $output.Contains($expected)) {
    throw ('Expected the SWIG prerequisite error, but received:' + [Environment]::NewLine + $output)
}

Write-Host 'Native build rejects a missing SWIG executable.'
